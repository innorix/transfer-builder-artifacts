# INNORIX Transfer Builder — Python

`combo_builder.py` runs the transfer configured in the INNORIX **Transfer Builder** and is configured entirely through a `.env` file.

> Full `.env` reference: **.env.builder.example**, bundled next to this file — every key with a comment.

**Links**

- [Quick Start](https://innorix.com/en/developer#dev-api-quick)
- [Examples](https://innorix.com/en/developer/example)
- [API Reference](https://innorix.com/en/developer/api)
- [Transfer Builder](https://innorix.com/en/)

## Requirements

- Python 3.8+
- `pip install requests`

## Run

```bash
pip install requests
cp .env.builder.example .env      # then edit .env
python combo_builder.py           # or: python combo_builder.py path/to/other.env
```

On **Windows (PowerShell)**, use:

```powershell
Copy-Item .env.builder.example .env
```

If `python` is not available on your PATH, use `py` instead.

Run the command from this folder so the script can locate `.env`, or pass a `.env` file path as the argument.

## Authentication

Configure **ONE** of the following in `.env`:

- `INNORIX_API_KEY=…` — Long-lived user API key sent as `x-api-key` (recommended)
- `INNORIX_ACCESS_TOKEN=…` — Short-lived JWT sent as `Authorization: Bearer`
- `INNORIX_EMAIL` + `INNORIX_PASSWORD` — The script logs in and retrieves an access token

## Get a long-lived API key

**Recommended method:** Open the profile menu at the bottom-left of the product interface, select **Developer**, and copy the **API Key** into `.env` as `INNORIX_API_KEY`.

Use the eye icon to reveal the key, the copy icon to copy it, or **Regenerate** to create a new key.

The same dialog also displays your **Workspace ID** (`INNORIX_WORKSPACE_ID`).

Leave `INNORIX_BASE_URL` set to its default value:

```text
https://app.innorix.com
```

Alternatively, create an API key through the API:

`POST /api/auth/api-keys`

This request requires a Bearer access token and no request body. The generated key is returned as `data.apiKey`.

## `.env` options

All `.env` configuration keys — transfer type, start conditions, after-transfer actions and file options — are listed with comments in **.env.builder.example**, bundled next to this file.
