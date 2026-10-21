# Send Files to Many Devices at a Scheduled Time With Follow-Up Actions

> Builder page: <https://innorix.com/en/builder/send-many-scheduled-follow-up>
> Pattern: **Send to many** · **All files** · **Scheduled time** · **Follow-up actions**

Run it once, and INNORIX registers a one-time job: at the date and time you set, the files in a folder on one device (the **source**) are sent to several devices (the **targets**) at once, one transfer per target. The script exits right after registering. The transfer runs later on its own, even if the PC you ran the script on is off. Every file in the folder is included, subfolders too. Around the transfer, the follow-up actions you picked run automatically: a Microsoft Defender virus scan before the files leave the source, and a Slack message when the transfer starts, completes or fails.

You do not write any code. The script in each language folder is written for this pattern; everything you need to supply lives in one `.env` file next to it.

---

## 1. What this example does

| Builder choice | This example | In `.env` |
|---|---|---|
| Transfer type | One source device → several target devices | `SOURCE_ID`, `SOURCE_PATH` → `TARGET_IDS`, `TARGET_PATHS` |
| Files | Every file in the source folder | no filter keys; `SAVE_PATH`, `DUPLICATE_ACTION`, `INTEGRITY` still apply |
| Start | Once, at a date and time you choose | `SCHEDULE_AT`, `SCHEDULE_TZ` |
| After transfer | Virus scan and Slack message are on; email, monitoring and run-API can be added | `ON_VIRUS_SCAN`, `ON_MESSAGE` (on), `ON_EMAIL`, `ON_MONITORING`, `ON_RUN_API` |

The script in this folder is written for exactly this pattern, so there is nothing to choose in the code: it reads `.env`, signs in to the INNORIX Public API, creates the follow-up integrations (`POST /api/integrations`), then creates one automation with a one-time schedule (`POST /api/automations`) and exits. The INNORIX server starts the transfer at that time. All device pairs go into one automation, so you get one job with several transfers inside it.

If you want more or less than this, a neighbouring example is one option away. See [Related examples](#related-examples).

---

## 2. What you need before you start

Tick these off first. Everything below is done in the INNORIX Platform console (<https://app.innorix.com>), not in code.

1. **An INNORIX Platform account.** Sign up for free at <https://innorix.com/en/> if you do not have one.
2. **Your API key.** In the console, open the profile menu at the bottom-left → **Developer** → copy the **API Key**. The same dialog shows your **Workspace ID**.
3. **One source device and at least two target devices**, all connected. Go to **Devices → Connect Device** for each one. A PC or server needs the INNORIX Agent installed; object storage (S3, Azure Blob, …) is connected with its own credentials. Every device must show as **online**. (The `.env` template lists three targets; two is enough to try it.)
4. **The device IDs.** In **Devices**, select a device; its ID is at the top-right. You need the source's ID and every target's ID.
5. **A source folder with a few test files** on the source device, and a **target folder path** that is valid on every target (the same path for all, or one path per target).
6. **A date and time in the future**, and the name of the time zone it is written in (for example `Asia/Seoul`, `America/New_York`, `Europe/Berlin`).
7. **For the follow-up actions:**
   - **Message (Slack):** a Slack *Incoming Webhook* URL for the channel that should get the message.
   - **Virus scan:** Microsoft Defender on the source device. Other scanners are accepted by the API but do not scan yet, so keep `VIRUS_SCAN_TYPE=microsoft_defender`.
8. **One runtime**, whichever language you prefer:

   | Language | Needs |
   |---|---|
   | Python | Python 3.8+ and `pip install requests` |
   | Node.js | Node.js 18+ (no packages to install) |
   | Java | Java 11+ (no build tool needed) |
   | C# | .NET 8 SDK |

---

## 3. Files in this folder

```text
send-many-scheduled-follow-up/
├── README.md                 ← you are here
├── python/
│   ├── combo_builder.py      ← the script, written for this pattern (do not edit)
│   ├── .env                  ← this pattern's settings; fill in the blanks
│   ├── .env.builder.example  ← every key explained, for reference
│   └── README.md             ← Python-specific notes
├── nodejs/   combo_builder.js  + the same three files
├── java/     ComboBuilder.java + the same three files
└── csharp/   ComboBuilder.cs, combo_builder.csproj + the same files
```

All four language folders behave identically. Pick one and ignore the rest.

---

## 4. Fill in `.env`

Open the language folder you chose. It already contains a `.env` for this pattern, with placeholders for your own values. Open it in any text editor and replace them. If the file is missing, create it from the template:

```bash
cp .env.builder.example .env            # macOS / Linux / Git Bash
```
```powershell
Copy-Item .env.builder.example .env     # Windows PowerShell
```

These are the parts of that file that matter for this example (the full file also lists the alternative sign-in options and every other key):

```ini
# ── Authentication — set ONE of these ───────────────────────────────────
#   INNORIX_API_KEY       long-lived user API key -> sent as the x-api-key header (recommended)
#   INNORIX_ACCESS_TOKEN  short-lived JWT          -> sent as Authorization: Bearer
#   INNORIX_EMAIL + INNORIX_PASSWORD               -> the script logs in for a token
INNORIX_API_KEY=
# INNORIX_WORKSPACE_ID=

# ── Devices ─────────────────────────────────────────────────────────────
# Use each device's deviceId (in the product: Devices -> select the device -> the ID at the top-right).
SOURCE_ID=<deviceId>
SOURCE_PATH=C:/data/out
TARGET_IDS=<deviceId>,<deviceId>,<deviceId>   # every target device, comma separated
TARGET_PATHS=C:/incoming,/data/incoming,/data/incoming   # one path for every target, or one per target in the same order
SOURCE_IS_DIR=true            # false when the source path is a single file
NAME=send-many-scheduled-follow-up

# ── When it runs ────────────────────────────────────────────────────────
# Once, at a specific date and time (local to SCHEDULE_TZ).
SCHEDULE_AT=2026-11-02T09:00:00
SCHEDULE_TZ=Asia/Seoul        # timezone of the automation

# ── File options ────────────────────────────────────────────────────────
SAVE_PATH=root                # root (no subfolder) | date (run-date subfolder) | device (subfolder per source device name)
# SAVE_PATH_DATE_FORMAT=%y%m%d   # folder name format for SAVE_PATH=date
DUPLICATE_ACTION=overwrite    # overwrite | rename | skip   (same file name at the target)
INTEGRITY=true                # verify each file after the transfer

# ── After transfer — follow-up actions (enable any; they may combine) ───
# Events that fire Message / Virus Scan / Email / Monitoring:
#   started, completed, paused, resumed, recovered, canceled, error, skipped
NOTIFY_EVENTS=completed,error

# Message (slack, microsoft_teams, discord, telegram, ...) — a workspace-wide integration.
#   Slack needs webhookUrl (and channel) in MESSAGE_CONFIG — without them it is saved but sends nothing.
#   MESSAGE_EVENTS overrides NOTIFY_EVENTS for this action only (blank = NOTIFY_EVENTS).
ON_MESSAGE=true
MESSAGE_TYPE=slack
MESSAGE_NAME=builder-message-slack
MESSAGE_EVENTS=started,completed,error
MESSAGE_CONFIG={"webhookUrl":"https://hooks.slack.com/services/XXX","channel":"#transfers"}

# Virus Scan — a workspace-wide security integration.
#   Only microsoft_defender is wired into the transfer pipeline today; other tools
#   (mcafee, crowdstrike, kaspersky, clamav, ...) are saved but do not scan.
#   VIRUS_SCAN_CONFIG (all required):
#     scanTiming  : before_transfer_source | after_transfer_target | both
#     onDetection : block_transfer | alert_only | admin_confirm   (stored; infected files are always skipped)
#     scanScope   : all | executable_files_only | custom_exceptions
ON_VIRUS_SCAN=true
VIRUS_SCAN_TYPE=microsoft_defender
VIRUS_SCAN_NAME=builder-virus-scan-defender
VIRUS_SCAN_CONFIG={"scanTiming":"before_transfer_source","onDetection":"block_transfer","scanScope":"all"}

# Email, Monitoring and Run API are also available here; see .env.builder.example.
```

### Where each value comes from

| Key | Put here | Where to find it |
|---|---|---|
| `INNORIX_API_KEY` | Your personal API key | Console → profile menu (bottom-left) → **Developer** |
| `INNORIX_WORKSPACE_ID` | Your workspace ID (optional) | Same dialog. Uncomment and set it if your account has more than one workspace |
| `SOURCE_ID` | The source device ID | Console → **Devices** → select device → ID at top-right |
| `TARGET_IDS` | All target device IDs, comma-separated | Same place, one per target |
| `SOURCE_PATH` | Folder on the source device | Any existing folder; use `/` even on Windows |
| `TARGET_PATHS` | One folder path for all targets, or one per target | Must be valid on each target; same order as `TARGET_IDS` |
| `SCHEDULE_AT` | Date and time of the single run | Must be in the future, 24-hour clock, no time-zone suffix |
| `SCHEDULE_TZ` | IANA time-zone name | The zone `SCHEDULE_AT` is written in, e.g. `America/New_York` |
| `MESSAGE_CONFIG` | JSON with the Slack webhook URL and channel | Slack → Apps → Incoming Webhooks |
| `VIRUS_SCAN_CONFIG` | When to scan and what to do on detection | Keep the defaults to scan before sending and stop on a hit |

Tips:

- `INNORIX_API_KEY` is the easiest way to authenticate. The file also accepts `INNORIX_ACCESS_TOKEN`, or `INNORIX_EMAIL` + `INNORIX_PASSWORD`; set only one.
- `.env` holds secrets. Never commit it or paste it into a ticket.
- Every key is explained line by line in `.env.builder.example`.

---

## 5. Run it

Run the command **inside the language folder**, so the script finds `.env`.

**Python**
```bash
pip install requests
python combo_builder.py
```

**Node.js**
```bash
node combo_builder.js
```

**Java**
```bash
java ComboBuilder.java
```

**C#**
```bash
dotnet run
```

To use a different `.env`, pass its path: `python combo_builder.py path/to/other.env` (C#: `dotnet run -- path/to/other.env`).

### What you should see

```text
[send-many-scheduled-follow-up]  3 device pair(s)
integrations:
  ON_MESSAGE: notification/slack 'builder-message-slack' created (I2025…)
  ON_VIRUS_SCAN: security/microsoft_defender 'builder-virus-scan-defender' created (I2025…)
automation created: A2025…
  schedule: once at 2026-11-02T09:00:00
```

If you see a line like this, the follow-up action was saved but is not yet usable:

```text
  ! ON_MESSAGE: MESSAGE_CONFIG is missing required fields: webhookUrl — the integration is saved but will not work
```

Add the listed fields to the JSON in `MESSAGE_CONFIG` (or `VIRUS_SCAN_CONFIG`) and run again. The script reuses the integration it already created, so running twice is safe.

### Confirm the result

1. **Right away, in the console:** open **Automations**; `send-many-scheduled-follow-up` is listed with its next run at the time you set. If the time or day looks wrong, `SCHEDULE_TZ` does not match the zone you meant.
2. **At the scheduled time:** the run appears under **Transfers**, then **History** once complete. Slack gets the start and completion messages.
3. **Files arrived:** the files are in `TARGET_PATHS` on **every** target device.
4. **Afterwards:** the automation stays in **Automations** but will not run again. Delete it, or edit its schedule in the console.

---

## 6. Something went wrong?

| What you see | What to do |
|---|---|
| `missing required setting in .env: …` | Fill in the key named in the message. Check you ran the command inside the language folder. |
| `set INNORIX_API_KEY or INNORIX_ACCESS_TOKEN, or …` | No credential is set. Paste your API key into `INNORIX_API_KEY`. |
| `API error 401` | The key is wrong or expired. Regenerate it in **Developer**; if you have several workspaces, set `INNORIX_WORKSPACE_ID`. |
| `API error 404` / device not found | A device ID is wrong or belongs to another workspace. Copy it again from **Devices**. |
| `TARGET_PATHS must have 1 entry (same for all) or exactly N` | Give one path (used on every target) or exactly one path per ID in `TARGET_IDS`, same order. |
| It ran at the wrong hour or day | `SCHEDULE_TZ` must be the zone `SCHEDULE_AT` is written in. Do not add `Z` or `+09:00` to `SCHEDULE_AT`. |
| Nothing happened at the time | The time was already in the past when you ran the script, or a device was offline at that moment. Check **Automations** → the run log. |
| `… is missing required fields` | Add the listed fields to the `*_CONFIG` JSON. To see all fields a provider needs: `GET /api/integrations/rules/<type>`. |
| The files are not scanned | Only `VIRUS_SCAN_TYPE=microsoft_defender` scans today; other scanners are saved but do nothing. Defender must be present on the source device. |
| `SAVE_PATH must be root|date|device` / `DUPLICATE_ACTION must be …` | Use one of the listed values exactly as written. |
| `cannot reach https://app.innorix.com` | Network or proxy. Node.js behind a proxy: `NODE_USE_ENV_PROXY=1 node combo_builder.js` (Node 22.21+ / 24+). |
| Anything else | Set `DEBUG=true` in `.env` and run again: every request and response is printed (secrets masked). |

---

## 7. Change it without touching the code

Everything below is a one-line change in `.env`. Anything beyond these keys (another transfer type, another start condition) is a different example: pick it from the table.

- **Include only some files:** this script has no filter keys; use the matching variant of this example instead (first row of the table below).
- **Message only on completion:** `MESSAGE_EVENTS=completed,error`.
- **Scan after arrival instead of before sending:** `"scanTiming":"after_transfer_target"` in `VIRUS_SCAN_CONFIG`.
- **Skip infected files but keep going:** `"onDetection":"alert_only"` (the default `block_transfer` stops the transfer).
- **Add more follow-ups:** `ON_MONITORING=true` (Grafana, Datadog, …), `ON_EMAIL=true`, `ON_RUN_API=true` + `RUN_API_URL` to call your own webhook. They can be combined.
- **Pick another time:** change `SCHEDULE_AT` and run again. Each run of the script creates a new automation; delete the old one in the console.
- **Different folder per target:** `TARGET_PATHS=C:/in,D:/in,/srv/in`, one per ID in `TARGET_IDS`, same order.
- **Keep the files in a dated subfolder:** `SAVE_PATH=date`. What to do with a file that already exists: `DUPLICATE_ACTION=overwrite|rename|skip`.

### Related examples

| If you want… | Use |
|---|---|
| The same, but only files matching a filter | [`send-matching-many-scheduled-follow-up/`](../send-matching-many-scheduled-follow-up/) |
| The same, without follow-up actions | [`send-many-scheduled/`](../send-many-scheduled/) |
| The same, but right now | [`send-many-now-follow-up/`](../send-many-now-follow-up/) |
| The same, but every day / week / month | [`send-many-recurring-follow-up/`](../send-many-recurring-follow-up/) |
| The same, but when another transfer finishes | [`send-many-after-transfer-follow-up/`](../send-many-after-transfer-follow-up/) |
| The same, but started by an HTTP request | [`send-many-on-request-follow-up/`](../send-many-on-request-follow-up/) |
| The same, but to one device | [`send-one-scheduled-follow-up/`](../send-one-scheduled-follow-up/) |
| The same, but collecting from several devices into one | [`collect-many-scheduled-follow-up/`](../collect-many-scheduled-follow-up/) |

Full index of all examples: [repository README](../README.md#builder-directory-index).

---

## Links

- [Transfer Builder](https://innorix.com/en/builder/send-many-scheduled-follow-up) — change the options and regenerate `.env`
- [Developer Center](https://innorix.com/en/developers) · [API Quickstart](https://innorix.com/en/developers#irxApiQuick) · [API Reference](https://innorix.com/en/docs/api)
- [Product Guide — Connect devices](https://innorix.com/en/product-guide#connect-devices)
