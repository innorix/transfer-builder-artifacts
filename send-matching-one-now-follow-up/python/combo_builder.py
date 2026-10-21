# =============================================================================
#  INNORIX Transfer Builder example — Send Matching Files to One Device Right Now With Follow-Up Actions
# -----------------------------------------------------------------------------
#  Result page : https://innorix.com/en/builder/send-matching-one-now-follow-up
#  Devices     : one source device -> one target device
#  Starts      : right now (the script waits for the run and prints the result)
#  Files       : only files that match FILTER_EXT / FILTER_MIN_MB / FILTER_MAX_MB
#  After       : follow-up actions (Run API, Monitoring, Message, Virus Scan, Email)
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


def env_float(key, default=None):
    value = env(key)
    if value is None:
        return default
    try:
        return float(value)
    except ValueError:
        sys.exit(f"{key} must be a number, got: {value}")


def env_list(key):
    return [v.strip() for v in (env(key) or "").split(",") if v.strip()]


def env_json(key, default=None):
    value = env(key)
    if value is None:
        return default
    try:
        return json.loads(value)
    except ValueError as exc:
        sys.exit(f"{key} must be valid JSON: {exc}")


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
    """DUPLICATE_ACTION / FILTER_* / SAVE_PATH -> transferOptions ; INTEGRITY -> bool."""
    # Duplicated Name — what to do when the target already has a file with the same name
    raw = env("DUPLICATE_ACTION", "overwrite").lower()
    action = DUPLICATE_MAP.get(raw)
    if action is None:
        sys.exit(f"DUPLICATE_ACTION must be overwrite|rename|skip, got: {raw}")
    options = {"target-action": action}

    # Matching files only — Type Filter (extensions, allow/deny) + Size Filter (MB)
    fo = {}
    exts = env_list("FILTER_EXT")
    if exts:
        allow = env("FILTER_MODE", "allow").lower() != "deny"
        fo["extension"] = {"extension": [e.lstrip(".").lower() for e in exts], "allow": allow}

    # send-fileoption.fileSize holds a single bound {size, over}. When both a min
    # and a max are given, send the MIN bound (>= size); FILTER_MAX_MB is not sent.
    min_mb, max_mb = env_float("FILTER_MIN_MB"), env_float("FILTER_MAX_MB")
    if min_mb is not None:
        fo["fileSize"] = {"size": int(min_mb * 1024 * 1024), "over": True, "equal": True}
        if max_mb is not None:
            print("  WARNING: both size bounds set, but the API's fileSize filter takes ONE bound — "
                  f"only FILTER_MIN_MB ({min_mb}MB) is applied and FILTER_MAX_MB ({max_mb}MB) is IGNORED, "
                  "so larger files will be transferred")
    elif max_mb is not None:
        fo["fileSize"] = {"size": int(max_mb * 1024 * 1024), "over": False, "equal": True}
    if fo:
        options["send-fileoption"] = fo

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


# ================================================================== after transfer (follow-up actions)

# Message / Virus Scan / Email are workspace-wide integrations (POST /api/integrations)
# that fire for the events in NOTIFY_EVENTS. Run API and Monitoring are processors
# attached to this automation (see after_transfer_processors()).
INTEGRATIONS = (   # toggle, category, .env prefix
    ("ON_MESSAGE", "notification", "MESSAGE"),
    ("ON_VIRUS_SCAN", "security", "VIRUS_SCAN"),
    ("ON_EMAIL", "email", "EMAIL"),
)


def notify_events(prefix=None):
    """Events for one action. <PREFIX>_EVENTS (per action) wins over the shared NOTIFY_EVENTS."""
    names = (env_list(f"{prefix}_EVENTS") if prefix else []) or env_list("NOTIFY_EVENTS") or ["completed", "error"]
    return {e: True for e in names}


def ensure_integrations(api):
    """Create (or reuse) one workspace-wide integration per enabled ON_* toggle."""
    for toggle, category, prefix in INTEGRATIONS:
        if not env_bool(toggle):
            continue
        events = notify_events(prefix)
        provider = require(f"{prefix}_TYPE").lower()
        config = env_json(f"{prefix}_CONFIG", {}) or {}
        if not isinstance(config, dict):
            sys.exit(f"{prefix}_CONFIG must be a JSON object")
        name = env(f"{prefix}_NAME") or config.get("name") or f"builder-{provider}"
        config.setdefault("name", name)

        # warn about missing required fields (the server saves it anyway, but it won't work)
        rules = api.call("GET", f"/api/integrations/rules/{provider}") or {}
        rule = rules.get(provider) or (next(iter(rules.values()), None) if rules else None)
        if not rule:
            print(f"  ! {toggle}: unknown provider '{provider}' — check {prefix}_TYPE")
        else:
            missing = [f["id"] for f in rule.get("fields") or []
                       if f.get("required") and f["id"] not in config]
            if missing:
                print(f"  ! {toggle}: {prefix}_CONFIG is missing required fields: "
                      f"{', '.join(missing)} — the integration is saved but will not work")

        # reuse an existing integration with the same type + name
        for item in api.call("GET", "/api/integrations", params={"category": category}) or []:
            if item.get("type") == provider and item.get("name") == name:
                print(f"  {toggle}: {category}/{provider} '{name}' already exists, reused")
                break
        else:
            body = {"name": name, "type": provider, "category": category, "config": config,
                    "notificationConfig": {"events": events}}
            for key in ("webhookUrl", "baseUrl", "mode", "authType"):
                if key in config:
                    body[key] = config[key]
            if "webhookUrl" in config:
                body.setdefault("mode", "webhook")
            result = api.call("POST", "/api/integrations", body) or {}
            print(f"  {toggle}: {category}/{provider} '{name}' created "
                  f"({result.get('integrationId') or result.get('id')})")


def run_api_processor():
    """ON_RUN_API -> one action processor on the automation.

    A run action needs category="run" and type="http"|"command"|"script", and every
    parameter goes under `config` (url / method / body / deviceId / events / waitForResult)."""
    if not env_bool("ON_RUN_API"):
        return None
    action = env("RUN_API_TYPE", "http").lower()
    if action == "https":
        action = "http"
    if action not in ("http", "command", "script"):
        sys.exit(f"RUN_API_TYPE must be http|command|script, got: {action}")

    config = {}
    if action == "http":
        config["url"] = require("RUN_API_URL")
        config["method"] = env("RUN_API_METHOD", "POST").upper()
        body = env("RUN_API_BODY")
        if body:
            parsed = env_json("RUN_API_BODY", None)
            if not isinstance(parsed, dict):
                sys.exit("RUN_API_BODY must be a JSON object, e.g. {\"key\":\"value\"}")
            config["body"] = parsed
    elif action == "command":
        config["command"] = require("RUN_API_COMMAND")
    else:
        config["file"] = require("RUN_API_SCRIPT")
        args = env("RUN_API_ARGS")
        if args:
            config["args"] = args

    # Run On: which device runs the action (omit -> the transfer's device)
    device = env("RUN_API_DEVICE", "").lower()
    if device == "source":
        config["deviceId"] = require("SOURCE_ID")
    elif device == "target":
        config["deviceId"] = require("TARGET_ID")
    elif device:
        config["deviceId"] = env("RUN_API_DEVICE")

    # config.events = {"completed": true, ...}; omit to fire on every event.
    events = env_list("RUN_API_EVENTS")
    if events:
        config["events"] = {e: True for e in events}
    if env_bool("RUN_API_WAIT", False):
        config["waitForResult"] = True

    return {
        "category": "run",
        "type": action,
        "name": env("RUN_API_NAME", "builder-run-api"),
        "config": config,
    }


def monitoring_processor():
    """ON_MONITORING -> a monitoring processor attached to the automation. Monitoring
    providers fire only when attached to an automation, scoped to its transfers."""
    if not env_bool("ON_MONITORING"):
        return None
    provider = require("MONITORING_TYPE").lower()
    config = env_json("MONITORING_CONFIG", {}) or {}
    if not isinstance(config, dict):
        sys.exit("MONITORING_CONFIG must be a JSON object")
    name = env("MONITORING_NAME") or config.get("name") or f"builder-{provider}"
    config.setdefault("name", name)
    return {
        "category": "monitoring",
        "type": provider,
        "name": name,
        "config": config,
        "notificationConfig": {"events": notify_events("MONITORING")},
    }


def after_transfer_processors():
    """Processors attached to the automation: Run API + Monitoring (each optional)."""
    return [p for p in (run_api_processor(), monitoring_processor()) if p]


# ================================================================== when it runs

def build_schedule():
    """Right now: an automation whose schedule is type=none, startDateType=now runs at once."""
    return {"type": "none", "startDateType": "now", "startDate": now_iso(),
            "timezone": env("SCHEDULE_TZ", "Asia/Seoul")}


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


def automation_body(name, pairs, options, integrity, is_dir, schedule, processors):
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
    if processors:
        body["processors"] = processors
    return body


# ================================================================== wait for the run

TERMINAL = {2, 4, 5, 9, 99}   # complete, error, cancelled, partial, fail
STATUS = {-1: "queued", 0: "waiting", 1: "started", 2: "complete", 3: "paused",
          4: "error", 5: "cancelled", 6: "transferring", 7: "skipped", 8: "retry",
          9: "partial-complete", 10: "retry-checking", 11: "virus-scanning",
          12: "syncing", 13: "incoming", 14: "activity", 99: "fail"}


def wait_for(api, monitor_id, timeout, interval=3, heartbeat=15):
    """Poll GET /api/transfers/{monitorId} until the transfer reaches a terminal
    status. Prints on every change, plus a heartbeat at least every `heartbeat`
    seconds; warns when there has been no progress for a while."""
    start = last_beat = last_change = time.time()
    deadline = start + timeout
    last_state = None
    while time.time() < deadline:
        detail = api.call("GET", f"/api/transfers/{monitor_id}") or {}
        status = detail.get("status")
        percent = detail.get("percent", detail.get("progress", 0))
        now = time.time()
        state = (status, percent)
        changed = state != last_state
        if changed:
            last_state, last_change = state, now
        if changed or (now - last_beat) >= heartbeat:
            stalled = int(now - last_change)
            note = (f"  — no progress for {stalled}s; check the target agent/connection"
                    if stalled >= 60 else "")
            print(f"    status: {STATUS.get(status, status)} ({percent}%)  "
                  f"[{int(now - start)}s elapsed]{note}")
            last_beat = now
        if detail.get("isTerminal", status in TERMINAL):
            return detail
        time.sleep(interval)
    raise TimeoutError(f"transfer {monitor_id} did not finish within {timeout}s "
                       "(still running — raise WAIT_TIMEOUT, or set WAIT=false to not wait)")


# Non-transfer summary row kinds in the active list — skip these; a real transfer
# row has type "monitor" (its top-level monitorId is the id to poll).
SKIP_ROW_TYPES = {"automation", "history", "flow"}


def _active_monitor_ids(api, automation_id):
    """monitorIds of this automation's active transfers (GET /api/transfers?automationId=...).
    data = {data:[records], pagination:{...}}; a transfer row is {type:'monitor', monitorId, ...}."""
    result = api.call("GET", "/api/transfers", params={"automationId": automation_id})
    records = result.get("data") if isinstance(result, dict) else result
    ids = []
    for r in records or []:
        if r.get("type") in SKIP_ROW_TYPES:
            continue
        mid = r.get("monitorId") or r.get("id")
        if mid:
            ids.append(mid)
    return ids


def resolve_monitor_ids(api, automation_id, expected, interval=3, appear_wait=120):
    """Poll until the run(s) start and collect their monitorIds (accumulated across
    polls, so a short transfer that finishes between polls is not missed)."""
    start = time.time()
    deadline = start + appear_wait
    seen = []
    while True:
        for mid in _active_monitor_ids(api, automation_id):
            if mid not in seen:
                seen.append(mid)
        if len(seen) >= expected or time.time() >= deadline:
            return seen[:expected] if len(seen) >= expected else seen
        print(f"    waiting for the run to start... ({len(seen)}/{expected}) "
              f"[{int(time.time() - start)}s]")
        time.sleep(interval)


def wait_for_run(api, automation_id, expected):
    """One transfer per device pair; returns the process exit code."""
    if not env_bool("WAIT", True):
        print("  (WAIT=false) not waiting; find the transfer with "
              f"GET /api/transfers?automationId={automation_id}")
        return 0
    print("  waiting for the run to start and complete (polling every 3s; set WAIT=false to skip)...")
    appear_wait = env_int("APPEAR_WAIT", 120)
    monitor_ids = resolve_monitor_ids(api, automation_id, expected, appear_wait=appear_wait)
    if not monitor_ids:
        print(f"  ERROR: no transfer started within {appear_wait}s — the automation was created but "
              f"nothing ran. Check with GET /api/transfers?automationId={automation_id}")
        return 1
    missing = expected - len(monitor_ids)
    if missing > 0:
        print(f"  ERROR: only {len(monitor_ids)} of {expected} transfer(s) started within "
              f"{appear_wait}s (raise APPEAR_WAIT if the devices are slow to pick the run up)")
    print(f"  run started: {len(monitor_ids)} transfer(s) — {', '.join(monitor_ids)}")
    failed = 0
    for monitor_id in monitor_ids:
        detail = wait_for(api, monitor_id, env_int("WAIT_TIMEOUT", 3600))
        status = detail.get("status")
        print(f"  finished {monitor_id}: {STATUS.get(status, status)} "
              f"({detail.get('fileCount', 0)} files)")
        if status != 2:
            failed += 1
    return 1 if (failed or missing > 0) else 0


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
    name = env("NAME") or "send-matching-one-now-follow-up"
    options, integrity = file_options()
    processors = after_transfer_processors()

    api = Api()
    if options.get(SAVEPATH_DEVICE_FLAG):
        load_device_names(api)
    print(f"[send-matching-one-now-follow-up]  {len(pairs)} device pair(s)")

    try:
        # follow-up integrations: workspace-wide, independent of the transfer
        if any(env_bool(t) for t, _, _ in INTEGRATIONS):
            print("integrations:")
            ensure_integrations(api)

        body = automation_body(name, pairs, options, integrity, is_dir, build_schedule(), processors)

        automation_id = api.call("POST", "/api/automations", body)["automationId"]
        print(f"automation created: {automation_id}")
        sys.exit(wait_for_run(api, automation_id, len(pairs)))

    except ApiError as exc:
        sys.exit(f"API error {exc.status}: {exc}")
    except TimeoutError as exc:
        sys.exit(str(exc))


if __name__ == "__main__":
    main()
