# Collect Matching Files from Many Devices Right Now

> Builder page: <https://innorix.com/en/builder/collect-matching-many-now>
> Pattern: **Collect from many** · **Matching files only** · **Right now** · **No follow-up actions**

Run it once, and the files in a folder on each of several devices (the **sources**) are gathered into one folder on a single device (the **target**), one transfer per source immediately. The script waits until the transfer finishes and prints the result. Only files that pass your filter are included (for example, everything except `.exe`, `.msi` and `.bat`).

You do not write any code. The script in each language folder is written for this pattern; everything you need to supply lives in one `.env` file next to it.

---

## 1. What this example does

| Builder choice | This example | In `.env` |
|---|---|---|
| Transfer type | Several source devices → one target device | `SOURCE_IDS`, `SOURCE_PATHS` → `TARGET_ID`, `TARGET_PATH` |
| Files | Only files matching the type and size filter | `FILTER_EXT`, `FILTER_MODE`, `FILTER_MIN_MB`, `FILTER_MAX_MB` |
| Start | Immediately when you run the script, then wait for the result | `WAIT`, `WAIT_TIMEOUT`, `APPEAR_WAIT` |
| After transfer | Nothing. The transfer just runs | none |

The script in this folder is written for exactly this pattern, so there is nothing to choose in the code: it reads `.env`, signs in to the INNORIX Public API, creates one automation that runs right away (`POST /api/automations`) and polls the transfer (`GET /api/transfers/{id}`) until it finishes. All device pairs go into one automation, so you get one job with several transfers inside it.

If you want more or less than this, a neighbouring example is one option away. See [Related examples](#related-examples).

---

## 2. What you need before you start

Tick these off first. Everything below is done in the INNORIX Platform console (<https://app.innorix.com>), not in code.

1. **An INNORIX Platform account.** Sign up for free at <https://innorix.com/en/> if you do not have one.
2. **Your API key.** In the console, open the profile menu at the bottom-left → **Developer** → copy the **API Key**. The same dialog shows your **Workspace ID**.
3. **At least two source devices and one target device**, all connected. Go to **Devices → Connect Device** for each one. A PC or server needs the INNORIX Agent installed; object storage (S3, Azure Blob, …) is connected with its own credentials. Every device must show as **online**. (The `.env` template lists three sources; two is enough to try it.)
4. **The device IDs.** In **Devices**, select a device; its ID is at the top-right. You need every source's ID and the target's ID.
5. **A folder with a few test files on each source device** (the same path on all of them, or one path per source), and a **target folder path** on the target device (it is created if it does not exist).
6. **One runtime**, whichever language you prefer:

   | Language | Needs |
   |---|---|
   | Python | Python 3.8+ and `pip install requests` |
   | Node.js | Node.js 18+ (no packages to install) |
   | Java | Java 11+ (no build tool needed) |
   | C# | .NET 8 SDK |

---

## 3. Files in this folder

```text
collect-matching-many-now/
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
SOURCE_IDS=<deviceId>,<deviceId>,<deviceId>   # every source device, comma separated
SOURCE_PATHS=C:/data/out,/data/out,/data/out   # one path for every source, or one per source in the same order
TARGET_ID=<deviceId>
TARGET_PATH=C:/collect
SOURCE_IS_DIR=true            # false when the source path is a single file
NAME=collect-matching-many-now

# ── When it runs ────────────────────────────────────────────────────────
# Right now. The script waits for the run and prints the result.
WAIT=true                     # false = create it and exit without waiting
WAIT_TIMEOUT=3600             # seconds to wait for the run to finish
APPEAR_WAIT=120               # seconds to wait for the run to start
SCHEDULE_TZ=Asia/Seoul        # timezone of the automation

# ── File options ────────────────────────────────────────────────────────
# Matching files only — Type Filter (extensions) and/or Size Filter (MB)
FILTER_EXT=exe,msi,bat        # extension list (blank = no type filter)
FILTER_MODE=deny              # allow = only these pass ; deny = these are blocked
FILTER_MIN_MB=                # keep files >= this size, e.g. 1
FILTER_MAX_MB=                # keep files <= this size, e.g. 500 (one bound only: MIN wins if both are set)
SAVE_PATH=root                # root (no subfolder) | date (run-date subfolder) | device (subfolder per source device name)
# SAVE_PATH_DATE_FORMAT=%y%m%d   # folder name format for SAVE_PATH=date
DUPLICATE_ACTION=overwrite    # overwrite | rename | skip   (same file name at the target)
INTEGRITY=true                # verify each file after the transfer
```

### Where each value comes from

| Key | Put here | Where to find it |
|---|---|---|
| `INNORIX_API_KEY` | Your personal API key | Console → profile menu (bottom-left) → **Developer** |
| `INNORIX_WORKSPACE_ID` | Your workspace ID (optional) | Same dialog. Uncomment and set it if your account has more than one workspace |
| `SOURCE_IDS` | All source device IDs, comma-separated | Console → **Devices** → select device → ID at top-right |
| `TARGET_ID` | The collecting device's ID | Same place |
| `SOURCE_PATHS` | One folder read on every source, or one per source | Must exist on each source; same order as `SOURCE_IDS` |
| `TARGET_PATH` | Folder on the target device | Created if missing; not `/` or empty |
| `FILTER_EXT` + `FILTER_MODE` | Extensions and whether they are blocked or allowed | Your choice. The Builder default blocks `exe,msi,bat` |
| `FILTER_MIN_MB` / `FILTER_MAX_MB` | Size bounds in megabytes (optional) | One bound is applied; if both are set, the minimum wins |

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
[collect-matching-many-now]  3 device pair(s)
automation created: A2025…
  waiting for the run to start and complete (polling every 3s; set WAIT=false to skip)...
  run started: 3 transfer(s) — T2025…1, T2025…2, T2025…3
    status: transferring (46%)  [9s elapsed]
    status: complete (100%)  [15s elapsed]
  finished T2025…1: complete (12 files)
  finished T2025…2: complete (12 files)
  finished T2025…3: complete (12 files)
```

### Confirm the result

1. **Files arrived:** the files are in `TARGET_PATH` on the target device, with the files from every source. Files that matched the deny list (`.exe`, `.msi`, `.bat`) are *not* there. Files with the same name from different sources overwrite each other by default; set `SAVE_PATH=device` to keep each source in its own subfolder.
2. **Console:** open **Transfers** (or **History**) in <https://app.innorix.com>; the run shows **Complete** with the file count.
3. **Automations:** an automation named `collect-matching-many-now` was created. It has already run; delete it or keep it to run again from the console.

---

## 6. Something went wrong?

| What you see | What to do |
|---|---|
| `missing required setting in .env: …` | Fill in the key named in the message. Check you ran the command inside the language folder. |
| `set INNORIX_API_KEY or INNORIX_ACCESS_TOKEN, or …` | No credential is set. Paste your API key into `INNORIX_API_KEY`. |
| `API error 401` | The key is wrong or expired. Regenerate it in **Developer**; if you have several workspaces, set `INNORIX_WORKSPACE_ID`. |
| `API error 404` / device not found | A device ID is wrong or belongs to another workspace. Copy it again from **Devices**. |
| `SOURCE_PATHS must have 1 entry (same for all) or exactly N` | Give one path (read on every source) or exactly one path per ID in `SOURCE_IDS`, same order. |
| `no transfer started within 120s` | The automation was created but no device picked it up. Check the devices are **online**; then raise `APPEAR_WAIT=300`. |
| `only 2 of 3 transfer(s) started` | One device is offline or slow. Check it in **Devices**; raise `APPEAR_WAIT` if it is just slow. |
| `no progress for 60s; check the target agent/connection` | The transfer stalled. Check the receiving device is online and the target folder is writable. |
| Blocked files still arrive | `FILTER_MODE` is `allow`, not `deny`, or the extension is missing from `FILTER_EXT`. Use `FILTER_MODE=deny` and `FILTER_EXT=exe,msi,bat`. |
| `SAVE_PATH must be root|date|device` / `DUPLICATE_ACTION must be …` | Use one of the listed values exactly as written. |
| `cannot reach https://app.innorix.com` | Network or proxy. Node.js behind a proxy: `NODE_USE_ENV_PROXY=1 node combo_builder.js` (Node 22.21+ / 24+). |
| Anything else | Set `DEBUG=true` in `.env` and run again: every request and response is printed (secrets masked). |

---

## 7. Change it without touching the code

Everything below is a one-line change in `.env`. Anything beyond these keys (another transfer type, another start condition) is a different example: pick it from the table.

- **Send only certain types instead of blocking some:** `FILTER_MODE=allow` and `FILTER_EXT=pdf,docx`.
- **Filter by size:** `FILTER_MIN_MB` or `FILTER_MAX_MB` (one bound is applied).
- **Add follow-up actions:** this script does not create integrations; use the follow-up variant of this example instead (second row of the table below).
- **Do not wait for the result:** `WAIT=false`. The script prints the automation ID and exits.
- **Keep each source apart:** `SAVE_PATH=device` creates one subfolder per source device under `TARGET_PATH`.
- **Keep the files in a dated subfolder:** `SAVE_PATH=date`. What to do with a file that already exists: `DUPLICATE_ACTION=overwrite|rename|skip`.

### Related examples

| If you want… | Use |
|---|---|
| The same, but every file (no filter) | [`collect-many-now/`](../collect-many-now/) |
| The same, plus follow-up actions (virus scan, Slack, …) | [`collect-matching-many-now-follow-up/`](../collect-matching-many-now-follow-up/) |
| The same, but once at a set date and time | [`collect-matching-many-scheduled/`](../collect-matching-many-scheduled/) |
| The same, but every day / week / month | [`collect-matching-many-recurring/`](../collect-matching-many-recurring/) |
| The same, but when another transfer finishes | [`collect-matching-many-after-transfer/`](../collect-matching-many-after-transfer/) |
| The same, but started by an HTTP request | [`collect-matching-many-on-request/`](../collect-matching-many-on-request/) |
| The same, but to one device | [`send-matching-one-now/`](../send-matching-one-now/) |
| The same, but to several target devices | [`send-matching-many-now/`](../send-matching-many-now/) |

Full index of all examples: [repository README](../README.md#builder-directory-index).

---

## Links

- [Transfer Builder](https://innorix.com/en/builder/collect-matching-many-now) — change the options and regenerate `.env`
- [Developer Center](https://innorix.com/en/developers) · [API Quickstart](https://innorix.com/en/developers#irxApiQuick) · [API Reference](https://innorix.com/en/docs/api)
- [Product Guide — Connect devices](https://innorix.com/en/product-guide#connect-devices)
