# =============================================================================
#  INNORIX Transfer Builder example — Send Files to One Device When the Previous Transfer Ends
# -----------------------------------------------------------------------------
#  Result page : https://innorix.com/en/builder/send-one-after-transfer
#  Devices     : one source device -> one target device
#  Starts      : when the automation PREV_AUTOMATION_ID finishes
#
#  Every setting lives in the .env file next to this script (see .env.builder.example).
#  Requirements:  Python 3.8+ and the requests package (pip install requests)
#  Run:
#    python combo_builder.py                # use the .env in this folder
#    python combo_builder.py other.env      # or point it at another .env
# =============================================================================

import json
import os
import sys
import time
from datetime import datetime

try:
    import requests
except ImportError:
    sys.exit("the 'requests' package is required:  pip install requests")


# ================================================================== .env

def load_env_file(path):
    """Minimal KEY=VALUE parser (comments, blank lines, quoted values).
    Values already set in the real environment win over the file."""
    with open(path, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            key, value = key.strip(), value.strip()
            # Strip an inline comment on an unquoted value: a whole-line comment
            # (KEY= # ...) becomes empty; ' #' elsewhere ends the value. A '#'
            # without a leading space (e.g. a URL fragment) stays part of the value.
            if value[:1] not in ("\"", "'"):
                if value.startswith("#"):
                    value = ""
                elif " #" in value:
                    value = value.split(" #", 1)[0].rstrip()
            if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
                value = value[1:-1]
            os.environ.setdefault(key, value)


def env(key, default=None):
    value = os.getenv(key)
    return default if value is None or not value.strip() else value.strip()


def env_bool(key, default=False):
    value = env(key)
    return default if value is None else value.lower() in ("1", "true", "yes", "on")


def env_int(key, default=None):
    value = env(key)
    if value is None:
        return default
    try:
        return int(value)
    except ValueError:
        sys.exit(f"{key} must be an integer, got: {value}")


def require(key):
    value = env(key)
    if value is None:
        sys.exit(f"missing required setting in .env: {key}")
    return value


# ================================================================== API client

class ApiError(RuntimeError):
    def __init__(self, status, message):
        super().__init__(message)
        self.status = status


class Api:
    """API-key / Bearer-token client with login and refresh, enough for this script."""

    def __init__(self):
        self.base = env("INNORIX_BASE_URL", "https://app.innorix.com").rstrip("/")
        self.timeout = env_int("INNORIX_TIMEOUT", 30)
        self.workspace = env("INNORIX_WORKSPACE_ID")
        self.debug = env_bool("DEBUG")   # DEBUG=true -> print every request + response
        # Two credentials: a long-lived user API key -> x-api-key header, or a
        # short-lived JWT access token (login / refresh) -> Authorization: Bearer.
        self.api_key = env("INNORIX_API_KEY")
        self.access = env("INNORIX_ACCESS_TOKEN")
        self.refresh = None
        if not self.api_key and not self.access:
            email, password = env("INNORIX_EMAIL"), env("INNORIX_PASSWORD")
            if not (email and password):
                sys.exit("set INNORIX_API_KEY or INNORIX_ACCESS_TOKEN, "
                         "or INNORIX_EMAIL and INNORIX_PASSWORD in .env")
            self.login(email, password)

    def login(self, email, password):
        r = requests.post(f"{self.base}/api/auth/login",
                          json={"email": email, "password": password}, timeout=self.timeout)
        if not r.ok:
            sys.exit(f"login failed ({r.status_code}): {r.text[:200]}")
        user = r.json()["data"]["user"]          # tokens are nested under data.user
        self.access, self.refresh = user["accessToken"], user.get("refreshToken")

    def renew(self):
        if not self.refresh:
            return False
        r = requests.post(f"{self.base}/api/auth/token/refresh",
                          headers={"X-Refresh-Token": self.refresh}, timeout=self.timeout)
        if not r.ok:
            return False
        data = r.json()["data"]
        self.access, self.refresh = data["accessToken"], data.get("refreshToken", self.refresh)
        return True

    def call(self, method, path, body=None, params=None, _retried=False):
        headers = {"Content-Type": "application/json"}
        if self.api_key:
            headers["x-api-key"] = self.api_key            # long-lived user API key
        else:
            headers["Authorization"] = f"Bearer {self.access}"   # JWT access token
        if self.workspace:
            headers["x-workspace-id"] = self.workspace
        if self.debug:
            # Show exactly what is being sent (secrets in headers are masked).
            safe = {k: ("***" if k in ("x-api-key", "Authorization") else v)
                    for k, v in headers.items()}
            print(f"\n>> {method} {self.base + path}")
            print(f"   headers: {safe}")
            if params:
                print(f"   query  : {params}")
            if body is not None:
                print("   body   : " + json.dumps(body, ensure_ascii=False, indent=2))
        try:
            r = requests.request(method, self.base + path, headers=headers,
                                 json=body, params=params, timeout=self.timeout)
        except requests.RequestException as exc:
            raise ApiError(0, f"cannot reach {self.base}: {exc.__class__.__name__}")
        if self.debug:
            print(f"<< {r.status_code} {r.reason}")
            print("   response: " + (r.text or "(empty)"))
        if r.status_code == 401 and not _retried and self.renew():
            return self.call(method, path, body, params, _retried=True)
        try:
            payload = r.json()
        except ValueError:
            payload = {}
        if not r.ok:
            # Surface the full response body so the caller can see WHY (e.g. which
            # field the server rejected), not just the generic status text.
            detail = payload.get("message") or payload.get("error") or r.text[:500]
            if payload:
                detail = f"{detail}  |  full response: {json.dumps(payload, ensure_ascii=False)}"
            raise ApiError(r.status_code, detail)
        return payload.get("data")


def plain_path(raw_path):
    """Automation details use plain absolute paths (forward slashes) in
    sourceItem[].filePath and targetPath — not the {deviceId}_ino_{base64} token."""
    return str(raw_path or "").replace("\\", "/")


def now_iso():
    return time.strftime("%Y-%m-%dT%H:%M:%S.000Z", time.gmtime())


# ================================================================== devices

def device_pairs():
    """One source -> one target: a single (source_id, source_path, target_id, target_path)."""
    source, source_path = require("SOURCE_ID"), require("SOURCE_PATH")
    target, target_path = require("TARGET_ID"), require("TARGET_PATH")
    return [(source, source_path, target, target_path)]


# ================================================================== file options

DUPLICATE_MAP = {"skip": "nosend", "overwrite": "overwrite", "rename": "numbering"}

# SAVE_PATH=device marks the options dict here; automation_detail() replaces it with the
# per-pair source device name (kept out of the request body).
SAVEPATH_DEVICE_FLAG = "__savepath_device"
DEVICE_NAMES = {}


def load_device_names(api):
    """deviceId -> device name (GET /api/devices, paginated). Needed for SAVE_PATH=device
    because savepath must carry the folder name itself."""
    page = 1
    while True:
        result = api.call("GET", "/api/devices", params={"page": page, "size": 100}) or {}
        for device in result.get("devices") or []:
            if device.get("deviceId"):
                DEVICE_NAMES[device["deviceId"]] = device.get("name") or device["deviceId"]
        last_page = result.get("last_page") or result.get("lastPage") or 1
        if page >= last_page or page >= 20:
            return
        page += 1


def file_options():
    """DUPLICATE_ACTION / SAVE_PATH -> transferOptions ; INTEGRITY -> bool."""
    # Duplicated Name — what to do when the target already has a file with the same name
    raw = env("DUPLICATE_ACTION", "overwrite").lower()
    action = DUPLICATE_MAP.get(raw)
    if action is None:
        sys.exit(f"DUPLICATE_ACTION must be overwrite|rename|skip, got: {raw}")
    options = {"target-action": action}

    # Save Path: `savepath` carries the TARGET SUBFOLDER NAME as a string, and
    # `optionPath` only records which naming mode produced it (1=run date, 2=device name).
    # The source folder structure is preserved under the target path either way.
    #   root   -> no subfolder (nothing sent)
    #   date   -> SAVE_PATH_DATE_FORMAT (default %y%m%d) of the local run date
    #   device -> the SOURCE device's name, resolved per pair in automation_detail()
    save = env("SAVE_PATH", "root").lower()
    if save in ("root", "false"):
        pass
    elif save == "date":
        fmt = env("SAVE_PATH_DATE_FORMAT", "%y%m%d")
        options["savepath"], options["optionPath"] = datetime.now().strftime(fmt), 1
    elif save == "device":
        options["optionPath"] = 2
        options[SAVEPATH_DEVICE_FLAG] = True
    else:
        sys.exit(f"SAVE_PATH must be root|date|device, got: {save}")

    return options, env_bool("INTEGRITY")


# ================================================================== when it runs

def build_schedule():
    """After the previous transfer: chained to PREV_AUTOMATION_ID via triggerAutomation.value."""
    return {"type": "none", "startDateType": "now", "startDate": now_iso(),
            "timezone": env("SCHEDULE_TZ", "Asia/Seoul"),
            "triggerAutomation": {"value": require("PREV_AUTOMATION_ID")}}


# ================================================================== automation body

def automation_detail(pair, options, integrity, is_dir):
    source, source_path, target, target_path = pair
    detail_options = dict(options)
    if detail_options.pop(SAVEPATH_DEVICE_FLAG, False):
        # subfolder per SOURCE device
        detail_options["savepath"] = DEVICE_NAMES.get(source, source)
    if integrity:
        detail_options["checkIntegrity"] = True
    # Plain absolute paths: sourceItem[].filePath for the source, targetPath for the
    # destination (both forward-slashed). targetPath must not be "/" or empty.
    item = {"filePath": plain_path(source_path), "isDir": is_dir}
    return {
        "senderId": source,
        "receiverId": target,
        "sourceItem": [item],
        "targetPath": plain_path(target_path),
        "step": 1,
        "transferOptions": {"noSchedule": False, **detail_options},
    }


def automation_body(name, pairs, options, integrity, is_dir, schedule):
    body = {
        "name": name,
        "flowName": name,
        "transferType": "normal",
        "timezone": env("SCHEDULE_TZ", "Asia/Seoul"),
        "details": [automation_detail(p, options, integrity, is_dir) for p in pairs],
        "schedules": [schedule],
        "step": 1,
        # False keeps the schedule as sent (True makes the server replace it with a
        # one-time "upcoming" run)
        "isUpcoming": False,
    }
    # workflow id shared by the chain (generated when FLOW_ID is blank)
    body["flowId"] = env("FLOW_ID") or f"builder-flow-{int(time.time())}"
    return body


# ================================================================== main

def main():
    env_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        os.path.dirname(os.path.abspath(__file__)), ".env")
    if os.path.exists(env_path):
        load_env_file(env_path)
    else:
        print(f"note: {env_path} not found, using the current environment only")

    pairs = device_pairs()
    is_dir = env_bool("SOURCE_IS_DIR", True)
    name = env("NAME") or "send-one-after-transfer"
    options, integrity = file_options()

    api = Api()
    if options.get(SAVEPATH_DEVICE_FLAG):
        load_device_names(api)
    print(f"[send-one-after-transfer]  {len(pairs)} device pair(s)")

    try:
        body = automation_body(name, pairs, options, integrity, is_dir, build_schedule())

        automation_id = api.call("POST", "/api/automations", body)["automationId"]
        print(f"automation created: {automation_id}")
        print(f"  chained after automation: {env('PREV_AUTOMATION_ID')} (flowId {body.get('flowId')})")

    except ApiError as exc:
        sys.exit(f"API error {exc.status}: {exc}")


if __name__ == "__main__":
    main()
