# Send Files to One Device Right Now With Follow-Up Actions

> Builder page: <https://innorix.com/en/builder/send-one-now-follow-up>
> Pattern: **Send to one** · **All files** · **Right now** · **Follow-up actions**

Run it once, and the files in a folder on one device (the **source**) are sent to a folder on another device (the **target**) immediately. The script waits until the transfer finishes and prints the result. Every file in the folder is included, subfolders too. Around the transfer, the follow-up actions you picked run automatically: a Microsoft Defender virus scan before the files leave the source, and a Slack message when the transfer starts, completes or fails.

You do not write any code. The script in each language folder is written for this pattern; everything you need to supply lives in one `.env` file next to it.

---

## 1. What this example does

| Builder choice | This example | In `.env` |
|---|---|---|
| Transfer type | One source device → one target device | `SOURCE_ID`, `SOURCE_PATH` → `TARGET_ID`, `TARGET_PATH` |
| Files | Every file in the source folder | no filter keys; `SAVE_PATH`, `DUPLICATE_ACTION`, `INTEGRITY` still apply |
| Start | Immediately when you run the script, then wait for the result | `WAIT`, `WAIT_TIMEOUT`, `APPEAR_WAIT` |
| After transfer | Virus scan and Slack message are on; email, monitoring and run-API can be added | `ON_VIRUS_SCAN`, `ON_MESSAGE` (on), `ON_EMAIL`, `ON_MONITORING`, `ON_RUN_API` |

The script in this folder is written for exactly this pattern, so there is nothing to choose in the code: it reads `.env`, signs in to the INNORIX Public API, creates the follow-up integrations (`POST /api/integrations`), then creates one automation that runs right away (`POST /api/automations`) and polls the transfer (`GET /api/transfers/{id}`) until it finishes.

If you want more or less than this, a neighbouring example is one option away. See [Related examples](#related-examples).

---

## 2. What you need before you start

Tick these off first. Everything below is done in the INNORIX Platform console (<https://app.innorix.com>), not in code.

1. **An INNORIX Platform account.** Sign up for free at <https://innorix.com/en/> if you do not have one.
2. **Your API key.** In the console, open the profile menu at the bottom-left → **Developer** → copy the **API Key**. The same dialog shows your **Workspace ID**.
3. **Two connected devices.** Go to **Devices → Connect Device** and connect the machine that has the files (source) and the machine that should receive them (target). A PC or server needs the INNORIX Agent installed; object storage (S3, Azure Blob, …) is connected with its own credentials. Both devices must show as **online**.
4. **The two device IDs.** In **Devices**, select a device; its ID is at the top-right. You need one for the source and one for the target.
5. **A source folder with a few test files** on the source device, and a **target folder path** on the target device (it is created if it does not exist).
6. **For the follow-up actions:**
   - **Message (Slack):** a Slack *Incoming Webhook* URL for the channel that should get the message.
   - **Virus scan:** Microsoft Defender on the source device. Other scanners are accepted by the API but do not scan yet, so keep `VIRUS_SCAN_TYPE=microsoft_defender`.
7. **One runtime**, whichever language you prefer:

   | Language | Needs |
   |---|---|
   | Python | Python 3.8+ and `pip install requests` |
   | Node.js | Node.js 18+ (no packages to install) |
   | Java | Java 11+ (no build tool needed) |
   | C# | .NET 8 SDK |

---

## 3. Files in this folder

```text
send-one-now-follow-up/
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
SOURCE_PATH=/data/out
TARGET_ID=<deviceId>
TARGET_PATH=/data/incoming
SOURCE_IS_DIR=true            # false when the source path is a single file
NAME=send-one-now-follow-up

# ── When it runs ────────────────────────────────────────────────────────
# Right now. The script waits for the run and prints the result.
WAIT=true                     # false = create it and exit without waiting
WAIT_TIMEOUT=3600             # seconds to wait for the run to finish
APPEAR_WAIT=120               # seconds to wait for the run to start
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
| `SOURCE_ID` / `TARGET_ID` | Device IDs | Console → **Devices** → select device → ID at top-right |
| `SOURCE_PATH` | Folder on the source device | Any existing folder; use `/` even on Windows (`C:/data/out`) |
| `TARGET_PATH` | Folder on the target device | Created if missing; not `/` or empty |
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
[send-one-now-follow-up]  1 device pair(s)
integrations:
  ON_MESSAGE: notification/slack 'builder-message-slack' created (I2025…)
  ON_VIRUS_SCAN: security/microsoft_defender 'builder-virus-scan-defender' created (I2025…)
automation created: A2025…
  waiting for the run to start and complete (polling every 3s; set WAIT=false to skip)...
  run started: 1 transfer(s) — T2025…
    status: started (0%)  [3s elapsed]
    status: transferring (46%)  [9s elapsed]
    status: complete (100%)  [15s elapsed]
  finished T2025…: complete (12 files)
```

If you see a line like this, the follow-up action was saved but is not yet usable:

```text
  ! ON_MESSAGE: MESSAGE_CONFIG is missing required fields: webhookUrl — the integration is saved but will not work
```

Add the listed fields to the JSON in `MESSAGE_CONFIG` (or `VIRUS_SCAN_CONFIG`) and run again. The script reuses the integration it already created, so running twice is safe.

### Confirm the result

1. **Files arrived:** the files are in `TARGET_PATH` on the target device.
2. **Console:** open **Transfers** (or **History**) in <https://app.innorix.com>; the run shows **Complete** with the file count.
3. **Slack:** the channel in `MESSAGE_CONFIG` got a message when the transfer started and another when it completed.
4. **Automations:** an automation named `send-one-now-follow-up` was created. It has already run; delete it or keep it to run again from the console.

---

## 6. Something went wrong?

| What you see | What to do |
|---|---|
| `missing required setting in .env: …` | Fill in the key named in the message. Check you ran the command inside the language folder. |
| `set INNORIX_API_KEY or INNORIX_ACCESS_TOKEN, or …` | No credential is set. Paste your API key into `INNORIX_API_KEY`. |
| `API error 401` | The key is wrong or expired. Regenerate it in **Developer**; if you have several workspaces, set `INNORIX_WORKSPACE_ID`. |
| `API error 404` / device not found | A device ID is wrong or belongs to another workspace. Copy it again from **Devices**. |
| `no transfer started within 120s` | The automation was created but no device picked it up. Check the devices are **online**; then raise `APPEAR_WAIT=300`. |
| `no progress for 60s; check the target agent/connection` | The transfer stalled. Check the receiving device is online and the target folder is writable. |
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
- **Do not wait for the result:** `WAIT=false`. The script prints the automation ID and exits.
- **Keep the files in a dated subfolder:** `SAVE_PATH=date`. What to do with a file that already exists: `DUPLICATE_ACTION=overwrite|rename|skip`.

### Related examples

| If you want… | Use |
|---|---|
| The same, but only files matching a filter | [`send-matching-one-now-follow-up/`](../send-matching-one-now-follow-up/) |
| The same, without follow-up actions | [`send-one-now/`](../send-one-now/) |
| The same, but once at a set date and time | [`send-one-scheduled-follow-up/`](../send-one-scheduled-follow-up/) |
| The same, but every day / week / month | [`send-one-recurring-follow-up/`](../send-one-recurring-follow-up/) |
| The same, but when another transfer finishes | [`send-one-after-transfer-follow-up/`](../send-one-after-transfer-follow-up/) |
| The same, but started by an HTTP request | [`send-one-on-request-follow-up/`](../send-one-on-request-follow-up/) |
| The same, but to several target devices | [`send-many-now-follow-up/`](../send-many-now-follow-up/) |
| The same, but collecting from several devices into one | [`collect-many-now-follow-up/`](../collect-many-now-follow-up/) |

Full index of all examples: [repository README](../README.md#builder-directory-index).

---

## Links

- [Transfer Builder](https://innorix.com/en/builder/send-one-now-follow-up) — change the options and regenerate `.env`
- [Developer Center](https://innorix.com/en/developers) · [API Quickstart](https://innorix.com/en/developers#irxApiQuick) · [API Reference](https://innorix.com/en/docs/api)
- [Product Guide — Connect devices](https://innorix.com/en/product-guide#connect-devices)
