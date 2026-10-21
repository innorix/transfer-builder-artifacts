# =============================================================================
#  INNORIX Transfer Builder example (combo_builder.py)  —  start-here guide
# -----------------------------------------------------------------------------
#  What is this script?
#    It runs the configuration you picked in the INNORIX "Transfer Builder"
#    (https://innorix.com/en/) directly as code. The settings live not in this
#    script but in the .env file next to it, so you can change only .env, leave
#    the code untouched, and run many kinds of transfer. (It calls the INNORIX
#    Public API.)
#
#  Requirements
#    1) Python 3.8+
#    2) the requests package  ->  pip install requests
#    3) a .env file in the same folder (credentials + transfer settings)
#
#  How to run
#    pip install requests
#    python combo_builder.py                # use the .env in this folder
#    python combo_builder.py other.env      # or point it at another .env
#
#  What it does (at a glance)
#    read .env -> log in (or use a token) -> build the matching API request ->
#    create the transfer/automation -> print the result (monitorId or automationId).
#
#  The .env has the same "two axes" as the Transfer Builder
#    Axis 1) TRANSFER_TYPE : who sends to whom
#         send_one   one-to-one           send_many  one-to-many distribution
#         collect    many-to-one          sync       realtime folder sync
#    Axis 2) START_WHEN : when it runs  (ignored for sync)
#         now        immediately          schedule   once at a set time
#         repeat     recurring (hour/day/week/month)  after_previous  after a prior automation
#         external   triggered by an external URL call
#    Plus: after-transfer actions (ON_*) and file options (FILTER_* / SAVE_PATH / ...).
#
#  For more detail see the README (README.md) and the
#  .env.builder.example file, which comments every setting. The English docstring
#  below is a technical summary of how each code block maps to the API.
# =============================================================================

"""Transfer Builder example — runs the transfer described in a .env file.

Builder combos : every option of the Transfer Builder in ONE script
Used by docs   : "Transfer Builder" download

    pip install requests
    cp .env.builder.example .env   # fill in what you set in the Transfer Builder
    python combo_builder.py   # or: python combo_builder.py path/to/other.env

This file is self-contained (no other example modules needed). It maps the two
axes of the Transfer Builder to the matching INNORIX Public API calls:

  AXIS 1  TRANSFER_TYPE   who sends to whom
        send_one   one source  -> one target
        send_many  one source  -> several targets   (TARGET_IDS / TARGET_PATHS)
        collect    several src  -> one target        (SOURCE_IDS / SOURCE_PATHS)
        sync       realtime folder watch (One-Way/Two-Way, on create/modify)

  AXIS 2  START_WHEN      when it runs   (ignored for TRANSFER_TYPE=sync)
        now             POST /api/automations  schedule type=none, startDateType=now
                        (immediate run)
        schedule        POST /api/automations  schedule type=none (one-time at SCHEDULE_AT)
        repeat          POST /api/automations  schedule type=hour|day|week|month
        after_previous  POST /api/automations  chained via triggerAutomation.value
        external        POST /api/automations  transferType=command (external URL request)

  AFTER TRANSFER   ON_RUN_API -> processors[] ; ON_MESSAGE / ON_VIRUS_SCAN /
        ON_MONITORING / ON_EMAIL -> workspace-wide POST /api/integrations
        (fire for the events in NOTIFY_EVENTS, not attached per transfer).

  FILE OPTIONS   FILTER_* / SAVE_PATH / DUPLICATE_ACTION -> transferOptions ;
        INTEGRITY -> checkIntegrity.
"""

import calendar
import json
import os
import sys
import time
from datetime import datetime, timedelta

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
    """Bearer-token client with login and refresh, enough for this script."""

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


TERMINAL = {2, 4, 5, 9, 99}   # complete, error, cancelled, partial, fail
STATUS = {-1: "queued", 0: "waiting", 1: "started", 2: "complete", 3: "paused",
          4: "error", 5: "cancelled", 6: "transferring", 7: "skipped", 8: "retry",
          9: "partial-complete", 10: "retry-checking", 11: "virus-scanning",
          12: "syncing", 13: "incoming", 14: "activity", 99: "fail"}


def wait_for(api, monitor_id, timeout, interval=3, heartbeat=15):
    """Poll GET /api/transfers/{monitorId} until the transfer reaches a terminal
    status. Prints on every change, plus a heartbeat at least every `heartbeat`
    seconds (with elapsed time) so a transfer that sits at the same percent still
    shows the script is alive; warns when there has been no progress for a while
    (e.g. the target agent is offline or the transfer stalled)."""
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


def plain_path(raw_path):
    """Automation details use plain absolute paths (forward slashes) in
    sourceItem[].filePath and targetPath — not the {deviceId}_ino_{base64} token."""
    return str(raw_path or "").replace("\\", "/")


def now_iso():
    return time.strftime("%Y-%m-%dT%H:%M:%S.000Z", time.gmtime())


# ================================================================== devices (axis 1)

def _expand_paths(paths, count, paths_key, ids_key):
    """Line a *_PATHS list up with its *_IDS list: 1 path = same for all, N = one each."""
    if not paths:
        sys.exit(f"{paths_key} is required for this transfer type (one path, "
                 f"or one per {ids_key})")
    if len(paths) == 1:
        return paths * count
    if len(paths) == count:
        return paths
    sys.exit(f"{paths_key} must have 1 entry (same for all) or exactly {count} "
             f"(one per {ids_key})")


def _resolve_list(list_ids_key, single_id_key, list_paths_key, single_path_key, kind):
    """Resolve the device list + its paths for send_many/collect, with a fallback to
    the single keys: if the *_IDS list is empty use the single *_ID as a one-device list,
    and if the *_PATHS list is empty use the single *_PATH."""
    ids = env_list(list_ids_key)
    if not ids:
        one = env(single_id_key)
        if not one:
            sys.exit(f"{kind} needs {list_ids_key} (or a single {single_id_key})")
        ids = [one]
    paths = env_list(list_paths_key)
    if not paths and env(single_path_key):
        paths = [env(single_path_key)]
    paths = _expand_paths(paths, len(ids), f"{list_paths_key} (or {single_path_key})", list_ids_key)
    return ids, paths


def device_pairs(transfer_type):
    """Expand TRANSFER_TYPE into a list of (source_id, source_path, target_id, target_path).

    Which keys each type needs:
      send_one / sync : SOURCE_ID + SOURCE_PATH  and  TARGET_ID + TARGET_PATH   (single pair)
      send_many       : one source (SOURCE_ID + SOURCE_PATH)  +  target list (TARGET_IDS + TARGET_PATHS)
      collect         : one target (TARGET_ID + TARGET_PATH)  +  source list (SOURCE_IDS + SOURCE_PATHS)
    For send_many/collect a *_PATHS with a single entry applies to every device in the list,
    and the single TARGET_ID/TARGET_PATH (or SOURCE_ID/SOURCE_PATH) is used as a fallback
    when the list keys are empty.
    """
    if transfer_type == "send_many":
        source, source_path = require("SOURCE_ID"), require("SOURCE_PATH")
        targets, paths = _resolve_list("TARGET_IDS", "TARGET_ID", "TARGET_PATHS", "TARGET_PATH", "send_many")
        return [(source, source_path, targets[i], paths[i]) for i in range(len(targets))]

    if transfer_type == "collect":
        target, target_path = require("TARGET_ID"), require("TARGET_PATH")
        sources, paths = _resolve_list("SOURCE_IDS", "SOURCE_ID", "SOURCE_PATHS", "SOURCE_PATH", "collect")
        return [(sources[i], paths[i], target, target_path) for i in range(len(sources))]

    # send_one, sync
    source, source_path = require("SOURCE_ID"), require("SOURCE_PATH")
    target, target_path = require("TARGET_ID"), require("TARGET_PATH")
    return [(source, source_path, target, target_path)]


# ================================================================== file options

DUPLICATE_MAP = {"skip": "nosend", "overwrite": "overwrite", "rename": "numbering",
                 "nosend": "nosend", "numbering": "numbering"}

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
    """FILTER_* / SAVE_PATH / DUPLICATE_ACTION -> transferOptions ; INTEGRITY -> bool."""
    raw = env("DUPLICATE_ACTION", "overwrite").lower()
    action = DUPLICATE_MAP.get(raw)
    if action is None:
        sys.exit(f"DUPLICATE_ACTION must be overwrite|rename|skip, got: {raw}")
    options = {"target-action": action}

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

    exclude = env("FILTER_EXCLUDE")
    if exclude:
        fo["fileName"] = {"name": exclude, "allow": False}
    if fo:
        options["send-fileoption"] = fo

    # Save Path: `savepath` carries the TARGET SUBFOLDER NAME as a string, and
    # `optionPath` only records which naming mode produced it (1=run date, 2=device
    # name, 3=custom). The server does NOT build the name from optionPath, so the
    # name must be sent here — a boolean would create a folder literally named "true".
    # The source folder structure is preserved under the target path either way.
    #   root   -> no subfolder (nothing sent)
    #   date   -> SAVE_PATH_DATE_FORMAT (default %y%m%d) of the local run date
    #   device -> the SOURCE device's name, resolved per pair in automation_detail()
    #   custom -> SAVE_PATH_NAME
    save = env("SAVE_PATH", "false").lower()
    if save in ("false", "off", "no", "0", "true", "on", "yes", "1", "root"):
        pass
    elif save in ("date", "yymmdd", "start_date"):
        fmt = env("SAVE_PATH_DATE_FORMAT", "%y%m%d")
        options["savepath"], options["optionPath"] = datetime.now().strftime(fmt), 1
    elif save in ("device", "device_name"):
        options["optionPath"] = 2
        options[SAVEPATH_DEVICE_FLAG] = True
    elif save in ("custom", "custom_name"):
        options["savepath"], options["optionPath"] = require("SAVE_PATH_NAME"), 3
    elif save in ("transfer-id", "transfer_id", "transferid"):
        sys.exit("SAVE_PATH=transfer-id is not supported: the target subfolder name must be known "
                 "before the transfer is created, so the transfer ID cannot be used. "
                 "Use date, device or custom.")
    else:
        sys.exit(f"SAVE_PATH must be root|date|device|custom (or true/false), got: {save}")

    return options, env_bool("INTEGRITY")


# ================================================================== after transfer

# Message / Virus Scan / Email fire workspace-wide (POST /api/integrations).
# Monitoring is the exception (spec v2.0.0): it fires only when attached to the
# automation as a monitoring processor — see monitoring_processor().
INTEGRATIONS = (   # toggle, category, .env prefix
    ("ON_MESSAGE", "notification", "MESSAGE"),
    ("ON_VIRUS_SCAN", "security", "VIRUS_SCAN"),
    ("ON_EMAIL", "email", "EMAIL"),
)


def notify_events(prefix=None):
    """Events for one action. <PREFIX>_EVENTS (per action, from the builder's Timing)
    wins over NOTIFY_EVENTS, which stays as the shared fallback."""
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

        # warn about missing required fields (server saves it anyway, but it won't work)
        rules = api.call("GET", f"/api/integrations/rules/{provider}") or {}
        rule = rules.get(provider) or (next(iter(rules.values()), None) if rules else None)
        if not rule:
            print(f"  ! {toggle}: unknown provider '{provider}' — check {prefix}_TYPE")
        else:
            missing = [f["id"] for f in rule.get("fields") or []
                       if f.get("required") and f["id"] not in config]
            if missing:
                detail = (f"{toggle}: {prefix}_CONFIG is missing required fields: "
                          f"{', '.join(missing)} — the integration is saved but will not work")
                if env_bool("INTEGRATION_STRICT", False):
                    sys.exit(f"  ! {detail} (INTEGRATION_STRICT=true)")
                print(f"  ! {detail}")

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

    Public API spec: a run action needs category="run" and type="http"|"command"|"script",
    and every parameter goes under `config` (url / method / body / deviceId / events /
    waitForResult). Top-level url/method are deprecated and ignored by the server, and a
    processor without category="run" is dropped silently."""
    if not env_bool("ON_RUN_API"):
        return None
    action = env("RUN_API_TYPE", "http").lower()
    if action == "https":                      # builder used to emit this; http is the spec value
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

    # Run On: source device = senderId, target device = receiverId (omit -> the transfer's device)
    device = env("RUN_API_DEVICE", "").lower()
    if device in ("source", "sender"):
        config["deviceId"] = require("SOURCE_ID")
    elif device in ("target", "receiver"):
        config["deviceId"] = require("TARGET_ID")
    elif device:
        config["deviceId"] = env("RUN_API_DEVICE")

    # config.events = {"completed": true, ...}; omit to fire on every event.
    # "run" is a mode label from the builder, not a transfer event, so it is ignored here.
    events = [e for e in env_list("RUN_API_EVENTS") if e.lower() != "run"]
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
    """ON_MONITORING -> a monitoring processor attached to the automation (spec v2.0.0).
    Monitoring providers (grafana/datadog/prometheus) are NOT dispatched workspace-wide;
    they fire only when attached to an automation, scoped to its transfers. Selectable
    events: started, completed, paused, recovered, deviceConnected, deviceDisconnected
    (empty = all)."""
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


# ================================================================== schedule (axis 2)

WEEKDAY = {"sun": "sunday", "mon": "monday", "tue": "tuesday", "wed": "wednesday",
           "thu": "thursday", "fri": "friday", "sat": "saturday"}
# datetime.weekday(): Monday=0 .. Sunday=6
WEEKDAY_INDEX = {"monday": 0, "tuesday": 1, "wednesday": 2, "thursday": 3,
                 "friday": 4, "saturday": 5, "sunday": 6}


def clock():
    return {"hour": env("REPEAT_HOUR", "12"), "minute": env("REPEAT_MINUTE", "00"),
            "ampm": env("REPEAT_AMPM", "am").lower(),
            "timezone": env("SCHEDULE_TZ", "Asia/Seoul")}


def _hour_24():
    return int(env("REPEAT_HOUR", "12")) % 12 + (12 if env("REPEAT_AMPM", "am").lower() == "pm" else 0)


def _now_in_tz(tz):
    """Current wall-clock time in tz (naive). Falls back to local time if the
    zoneinfo database is unavailable (Python < 3.9 without backports)."""
    try:
        from zoneinfo import ZoneInfo
        return datetime.now(ZoneInfo(tz)).replace(tzinfo=None)
    except Exception:
        print(f"  note: timezone '{tz}' unavailable; computing first run in local time")
        return datetime.now()


def next_occurrence(every, weekdays, doms):
    """First future time matching the repeat cadence + clock, in SCHEDULE_TZ —
    used to start 'on schedule' with no immediate run (REPEAT_RUN_NOW=false)."""
    now = _now_in_tz(env("SCHEDULE_TZ", "Asia/Seoul"))
    if every == "hour":                             # every hour at :00 (the UI has no minute)
        top = now.replace(minute=0, second=0, microsecond=0)
        while top <= now:                           # never report a first run in the past
            top += timedelta(hours=1)
        return top
    base = now.replace(hour=_hour_24(), minute=int(env("REPEAT_MINUTE", "00")),
                       second=0, microsecond=0)
    for i in range(0, 400):                         # scan forward day by day
        cand = base + timedelta(days=i)
        if cand <= now:
            continue
        if every == "day":
            return cand
        if every == "week" and cand.weekday() in weekdays:
            return cand
        if every == "month":
            last = calendar.monthrange(cand.year, cand.month)[1]
            if cand.day in doms or (0 in doms and cand.day == last):
                return cand
    return base


def _start_fields(every, weekdays, doms):
    """Choose startDateType/startDate. REPEAT_START_AT wins; else REPEAT_RUN_NOW
    decides. Default is start-on-schedule with NO immediate run (specific at the
    next occurrence); set REPEAT_RUN_NOW=true to also fire once on creation."""
    start_at = env("REPEAT_START_AT")
    if start_at:
        return {"startDateType": "specific", "startDate": start_at}
    if env_bool("REPEAT_RUN_NOW", False):
        return {"startDateType": "now", "startDate": now_iso()}
    first = next_occurrence(every, weekdays, doms).strftime("%Y-%m-%dT%H:%M:%S")
    print(f"  first run at {first} ({env('SCHEDULE_TZ', 'Asia/Seoul')}); no immediate run")
    return {"startDateType": "specific", "startDate": first}


def build_repeat_schedule():
    """START_WHEN=repeat -> schedule type hour|day|week|month (builder 'Every ...')."""
    every = env("REPEAT_EVERY", "day").lower()
    if every not in ("hour", "day", "week", "month"):
        sys.exit(f"REPEAT_EVERY must be hour|day|week|month, got: {every}")

    weekdays, doms = set(), set()
    sched = {"type": every, **clock()}
    if every == "week":
        names = []
        for d in env_list("REPEAT_WEEKDAY") or ["monday"]:
            key = d.lower()[:3]
            if key not in WEEKDAY:
                sys.exit(f"REPEAT_WEEKDAY: invalid weekday '{d}' (sunday..saturday)")
            names.append(WEEKDAY[key])
            weekdays.add(WEEKDAY_INDEX[WEEKDAY[key]])
        sched["dayInWeek"] = names
    elif every == "month":
        dates = []
        for d in env_list("REPEAT_DAY_OF_MONTH") or ["1"]:
            if d.strip().lower() in ("lastday", "last", "last_day", "eom"):
                d = "0"                              # the builder's "Last day" -> 0
            if not d.isdigit() or not 0 <= int(d) <= 31:
                sys.exit(f"REPEAT_DAY_OF_MONTH: invalid day '{d}' (1-31, 0 or 'lastday' = last day)")
            dates.append(str(int(d)))
            doms.add(int(d))
        sched["dayInMonth"] = dates

    sched.update(_start_fields(every, weekdays, doms))
    return sched


def _clock_from(stamp):
    """hour/minute/ampm for a 'YYYY-MM-DDTHH:MM[:SS]' stamp (12-hour clock, as the API wants)."""
    try:
        t = datetime.strptime(stamp[:16], "%Y-%m-%dT%H:%M")
    except ValueError:
        return {}
    hour12 = t.hour % 12 or 12
    return {"hour": f"{hour12:02d}", "minute": f"{t.minute:02d}", "ampm": "pm" if t.hour >= 12 else "am"}


def build_schedule(start_when):
    """Return the schedules[] object for the chosen START_WHEN (or None for 'now')."""
    tz = env("SCHEDULE_TZ", "Asia/Seoul")
    if start_when == "schedule":
        # One-time run at a specific date/time. The clock fields must be sent as well:
        # with only startDate the server falls back to its default clock (12 pm UTC) and
        # schedules the run at the wrong hour — and on the wrong day for a future date.
        at = require("SCHEDULE_AT")
        return {"type": "none", "startDateType": "specific",
                "startDate": at, "timezone": tz, **_clock_from(at)}
    if start_when == "repeat":
        return build_repeat_schedule()
    if start_when == "after_previous":
        # chained: the server sets type=triggerSchedule from triggerAutomation.value
        return {"type": "none", "startDateType": "now", "startDate": now_iso(), "timezone": tz,
                "triggerAutomation": {"value": require("PREV_AUTOMATION_ID")}}
    if start_when == "external":
        # Command trigger: only an external request starts it. startDateType must NOT be
        # "now" — that makes the server run the automation once at creation time.
        return {"type": "none", "startDateType": "specific", "startDate": now_iso(), "timezone": tz}
    return None


# ================================================================== automation body

def automation_detail(pair, options, integrity, is_dir, is_sync):
    source, source_path, target, target_path = pair
    detail_options = dict(options)
    if detail_options.pop(SAVEPATH_DEVICE_FLAG, False):
        # subfolder per SOURCE device, so a collect (N:1) keeps each sender separate
        detail_options["savepath"] = DEVICE_NAMES.get(source, source)
    if integrity:
        detail_options["checkIntegrity"] = True
    if is_sync:
        detail_options["syncType"] = 2 if env("SYNC_DIRECTION", "one_way").lower() == "two_way" else 1
        watch = env("SYNC_WATCH", "file_created").lower()
        # 1=on create, 2=on modify, 3=both (both requires the server to accept 3)
        watch_map = {"file_created": 1, "file_modified": 2, "both": 3}
        if watch not in watch_map:
            sys.exit(f"SYNC_WATCH must be file_created|file_modified|both, got: {watch}")
        detail_options["watchFolderType"] = watch_map[watch]
    # Plain absolute paths: sourceItem[].filePath for the source, targetPath for the
    # destination (both forward-slashed). The server reads filePath as the source path
    # (and, for sync/watch, as the watched folder); targetPath must not be "/" or empty.
    item = {"filePath": plain_path(source_path), "isDir": is_dir}
    return {
        "senderId": source,          # alias sourceId also accepted
        "receiverId": target,        # alias targetId also accepted
        "sourceItem": [item],
        "targetPath": plain_path(target_path),
        "step": 1,
        "transferOptions": {"noSchedule": False, **detail_options},
    }


def automation_body(name, pairs, options, integrity, is_dir, schedule, processors, is_sync):
    transfer_type = "sync" if is_sync else ("command" if env("START_WHEN") == "external" else "normal")
    body = {
        "name": name,
        "flowName": name,
        "transferType": transfer_type,
        "timezone": env("SCHEDULE_TZ", "Asia/Seoul"),
        "details": [automation_detail(p, options, integrity, is_dir, is_sync) for p in pairs],
        "schedules": [schedule or {"type": "none", "startDateType": "now",
                                   "startDate": now_iso(), "timezone": env("SCHEDULE_TZ", "Asia/Seoul")}],
        "step": 1,
        # False keeps a repeating/chained schedule as sent (True makes the server replace it
        # with a one-time "upcoming" run)
        "isUpcoming": False,
    }
    flow_id = env("FLOW_ID")
    if env("START_WHEN") == "after_previous" and not flow_id:
        flow_id = f"builder-flow-{int(time.time())}"
    if flow_id:
        body["flowId"] = flow_id
    if processors:
        body["processors"] = processors
    return body


# ================================================================== immediate transfer

def _wait_all(api, monitor_ids):
    """Poll each monitorId to completion; return non-zero if any did not complete."""
    failed = 0
    for monitor_id in monitor_ids:
        detail = wait_for(api, monitor_id, env_int("WAIT_TIMEOUT", 3600))
        status = detail.get("status")
        print(f"  finished {monitor_id}: {STATUS.get(status, status)} "
              f"({detail.get('fileCount', 0)} files)")
        if status != 2:
            failed += 1
    return 1 if failed else 0


# Non-transfer summary row kinds in the active list — skip these; a real transfer
# row has type "monitor" (its top-level monitorId is the id to poll).
SKIP_ROW_TYPES = {"automation", "history", "flow"}


def _active_monitor_ids(api, automation_id):
    """monitorIds of this automation's currently-active transfers, from the
    active-transfer list GET /api/transfers?automationId=... . This shows a transfer
    as soon as it STARTS (unlike the execution log, which reflects finished runs).
    The response is cursor-paginated: data = {data:[records], pagination:{...}} and a
    transfer row looks like {type:'monitor', monitorId:'T...', status:1, ...}."""
    result = api.call("GET", "/api/transfers", params={"automationId": automation_id})
    records = result.get("data") if isinstance(result, dict) else result
    ids = []
    for r in records or []:
        if r.get("type") in SKIP_ROW_TYPES:           # skip automation/history summary rows
            continue
        mid = r.get("monitorId") or r.get("id")       # monitorId (== id) is what we poll
        if mid:
            ids.append(mid)
    return ids


def resolve_monitor_ids(api, automation_id, expected, interval=3, appear_wait=120):
    """After an immediate automation is created, poll the active-transfer list until
    its run(s) start and collect their monitorIds (accumulated across polls, so a
    short transfer that finishes between polls is not missed), then those monitorIds
    are polled for progress like any transfer."""
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


def run_immediate_automation(api, name, pairs, options, integrity, is_dir, processors):
    """START_WHEN=now -> ONE POST /api/automations with a type=none, startDateType=now
    schedule (runs immediately). All device pairs go in details[]; the run's
    monitorId(s) come from the execution log."""
    tz = env("SCHEDULE_TZ", "Asia/Seoul")
    schedule = {"type": "none", "startDateType": "now", "startDate": now_iso(), "timezone": tz}
    body = automation_body(name, pairs, options, integrity, is_dir, schedule, processors, False)
    automation_id = api.call("POST", "/api/automations", body)["automationId"]
    print(f"  automation created: {automation_id}  (immediate: schedule type=none, startDateType=now)")

    if not env_bool("WAIT", True):
        print("  (WAIT=false) not waiting; find the transfer with "
              f"GET /api/transfers?automationId={automation_id}")
        return 0
    print("  waiting for the run to start and complete (polling every 3s; set WAIT=false to skip)...")
    appear_wait = env_int("APPEAR_WAIT", 120)
    monitor_ids = resolve_monitor_ids(api, automation_id, len(pairs), appear_wait=appear_wait)
    if not monitor_ids:
        print(f"  ERROR: no transfer started within {appear_wait}s — the automation was created but "
              f"nothing ran. Check with GET /api/transfers?automationId={automation_id}")
        return 1
    missing = len(pairs) - len(monitor_ids)
    if missing > 0:
        print(f"  ERROR: only {len(monitor_ids)} of {len(pairs)} transfer(s) started within "
              f"{appear_wait}s (raise APPEAR_WAIT if the devices are slow to pick the run up)")
    print(f"  run started: {len(monitor_ids)} transfer(s) — {', '.join(monitor_ids)}")
    failed = _wait_all(api, monitor_ids)
    return 1 if (failed or missing > 0) else 0


# ================================================================== main

TRANSFER_TYPES = ("send_one", "send_many", "collect", "sync")
START_WHENS = ("now", "schedule", "repeat", "after_previous", "external")


def main():
    env_path = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        os.path.dirname(os.path.abspath(__file__)), ".env")
    if os.path.exists(env_path):
        load_env_file(env_path)
    else:
        print(f"note: {env_path} not found, using the current environment only")

    transfer_type = env("TRANSFER_TYPE", "send_one").lower()
    if transfer_type not in TRANSFER_TYPES:
        sys.exit(f"TRANSFER_TYPE must be {'|'.join(TRANSFER_TYPES)}, got: {transfer_type}")
    is_sync = transfer_type == "sync"

    start_when = env("START_WHEN", "now").lower()
    if not is_sync and start_when not in START_WHENS:
        sys.exit(f"START_WHEN must be {'|'.join(START_WHENS)}, got: {start_when}")

    pairs = device_pairs(transfer_type)
    is_dir = env_bool("SOURCE_IS_DIR", True)
    name = env("NAME") or f"builder {transfer_type} {start_when}"
    options, integrity = file_options()
    processors = after_transfer_processors()

    api = Api()
    if options.get(SAVEPATH_DEVICE_FLAG):
        load_device_names(api)
    label = "sync" if is_sync else start_when
    print(f"[{transfer_type} / {label}]  {len(pairs)} device pair(s)")

    try:
        # AFTER TRANSFER integrations: workspace-wide, independent of the transfer
        if any(env_bool(t) for t, _, _ in INTEGRATIONS):
            print("integrations:")
            ensure_integrations(api)

        # Immediate transfer (only for non-sync START_WHEN=now):
        # POST /api/automations with a schedule of type=none, startDateType=now.
        if not is_sync and start_when == "now":
            print("transfer:")
            sys.exit(run_immediate_automation(api, name, pairs, options, integrity, is_dir, processors))

        # Everything else -> an automation
        schedule = None if is_sync else build_schedule(start_when)
        body = automation_body(name, pairs, options, integrity, is_dir, schedule, processors, is_sync)

        if not is_sync and start_when == "external":
            code = (api.call("POST", "/api/command/generate-code") or {}).get("code")
            api_key = (api.call("GET", "/api/command/generate-api-key") or {}).get("apiKey")
            if not code or not api_key:
                sys.exit("failed to issue command code / api key")
            body["code"], body["apiKey"] = code, api_key

        automation_id = api.call("POST", "/api/automations", body)["automationId"]
        print(f"automation created: {automation_id}")

        if is_sync:
            direction = "two-way" if env("SYNC_DIRECTION", "one_way").lower() == "two_way" else "one-way"
            watch = {"file_created": "on file create", "file_modified": "on file modify",
                     "both": "on create and modify"}.get(env("SYNC_WATCH", "file_created").lower())
            print(f"  sync: {direction}, {watch}")
        elif start_when == "schedule":
            print(f"  schedule: once at {env('SCHEDULE_AT')}")
        elif start_when == "repeat":
            print(f"  repeat: every {env('REPEAT_EVERY', 'day')} "
                  f"{json.dumps({k: v for k, v in schedule.items() if k not in ('startDate', 'startDateType')})}")
        elif start_when == "after_previous":
            print(f"  chained after automation: {env('PREV_AUTOMATION_ID')} (flowId {body.get('flowId')})")
        elif start_when == "external":
            trigger_base = env("INNORIX_TRIGGER_BASE_URL", api.base).rstrip("/")
            url = f"{trigger_base}/command/{body['code']}"
            print(f"  endpoint URL : {url}")
            print(f"  x-api-key    : {body['apiKey']}   (secret — anyone with it can start this transfer)")
            if env_bool("FIRE_TRIGGER"):
                r = requests.post(url, headers={"x-api-key": body["apiKey"]}, timeout=api.timeout)
                print(f"  trigger fired: {r.status_code} {r.text[:120]}")

    except ApiError as exc:
        sys.exit(f"API error {exc.status}: {exc}")
    except TimeoutError as exc:
        sys.exit(str(exc))


if __name__ == "__main__":
    main()
