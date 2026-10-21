# INNORIX Transfer Builder — Node.js Guide

This guide explains how to configure and run the Node.js implementation of an INNORIX Transfer Builder example.

`combo_builder.js` runs the transfer configured in the INNORIX **Transfer Builder**. The program reads its settings from a `.env` file, so you can adjust the transfer configuration without editing the JavaScript source code.

Each builder example has its own `README.md` describing the transfer scenario and its required settings. Use this guide for the Node.js runtime, configuration-file preparation, authentication, execution, and common troubleshooting steps.

## Before You Start

Make sure you have the following before running the example:

- An INNORIX Platform account.
- An API key or another supported authentication method.
- The Workspace ID for the workspace you will use.
- Registered and connected source and destination devices, as required by your selected transfer scenario.
- The files for the selected builder's Node.js directory.

New to INNORIX? Start with the [repository README](../../README.md) for the overall setup process. For the selected transfer scenario and its required configuration, read the builder-specific [README](../README.md).

## Requirements

- **Node.js 18 or later**
- A terminal or command-line interface
- Access to the selected builder's Node.js directory

The program uses Node.js's built-in global `fetch` and does not require external npm packages. You do not need to run `npm install` for this example.

Verify your installed Node.js version:

```bash
node --version
```

Make sure the version is 18 or later.

### Using an HTTP Proxy

Node.js's built-in `fetch` does not automatically use `HTTP_PROXY` or `HTTPS_PROXY` environment variables in all supported versions. If your environment requires an HTTP proxy, use the supported proxy configuration for your Node.js version.

`NODE_USE_ENV_PROXY` is available in Node.js 24.0 and later, and in 22.21 and later. Check with `node --version`. On a supported version, run:

**macOS / Linux**

```bash
NODE_USE_ENV_PROXY=1 node combo_builder.js
```

**Windows PowerShell**

```powershell
$env:NODE_USE_ENV_PROXY = "1"
node combo_builder.js
```

On Node.js 18.0 to 22.20 this variable has no effect and the script cannot reach the API through a proxy; upgrade Node.js in that case.

If you do not use a proxy, you can skip this section.

## 1. Understand the Files

The selected Node.js directory typically contains these files:

| File | Purpose |
|---|---|
| `combo_builder.js` | Node.js program that submits and monitors the configured operation. |
| `.env` | Builder-generated configuration for the selected scenario. Authentication values must be supplied by you. |
| `.env.builder.example` | General configuration template documenting the supported settings and their meanings. |
| `README.md` | Node.js execution guide for this example. |

### Keep the Generated `.env`

If the Transfer Builder provided a `.env` file with the selected example, **keep that file and edit it**. It contains the settings selected in the builder. Replacing it with the general `.env.builder.example` template may lose those selections.

If `.env` is missing, you can create one from the template and then configure it for your scenario:

**macOS / Linux**

```bash
cp .env.builder.example .env
```

**Windows PowerShell**

```powershell
Copy-Item .env.builder.example .env
```

The template describes all supported options; it is not necessarily a ready-to-run configuration. Refer to the selected builder's README for the values required by that scenario.

## 2. Review Your `.env` Before Running

Open `.env` and verify that its values match the transfer you intend to run. The available settings depend on the selected transfer scenario.

Common settings include:

| Setting | Purpose |
|---|---|
| `SOURCE_ID` / `SOURCE_PATH` | Identifies the source device and the path on that device. |
| `TARGET_ID` / `TARGET_PATH` | Identifies the destination device and path. |
| `INNORIX_WORKSPACE_ID` | Specifies the workspace used for API requests. |

Replace placeholders such as `<deviceId>` and example paths with values from your INNORIX Platform environment. For `send_many` and `collect`, additional device lists may be required; consult `.env.builder.example` and the selected builder's README.

### This Example's `.env` Settings

Builder page: [send-matching-many-now](https://innorix.com/en/builder/send-matching-many-now) — one source device → several target devices, runs right away and waits for the result; only files that match the filter are sent.

The transfer type and start condition are fixed in this example's code, so `TRANSFER_TYPE` and `START_WHEN` are not used. Set these keys in `.env`:

| Key | Required | Value in `.env` | Description |
| --- | --- | --- | --- |
| `INNORIX_API_KEY` | ✔ (or token / login) | — | User API key, sent as `x-api-key` (or set `INNORIX_ACCESS_TOKEN`, or `INNORIX_EMAIL` + `INNORIX_PASSWORD`) |
| `INNORIX_WORKSPACE_ID` |  | — | Workspace to act in |
| `SOURCE_ID` | ✔ | `<deviceId>` | Source device ID |
| `SOURCE_PATH` | ✔ | `C:/data/out` | Path on the source device |
| `TARGET_IDS` | ✔ | `<deviceId>,<deviceId>,<deviceId>` | Target device IDs, comma separated |
| `TARGET_PATHS` | ✔ | `C:/incoming,/data/incoming,/data/incoming` | One path for every target, or one per target in the same order |
| `SOURCE_IS_DIR` |  | `true` | `false` when the source path is a single file |
| `NAME` |  | `send-matching-many-now` | Automation name |
| `WAIT` |  | `true` | Wait for the run to finish and print the result |
| `WAIT_TIMEOUT` |  | `3600` | Seconds to wait for the run to finish |
| `APPEAR_WAIT` |  | `120` | Seconds to wait for the run to start |
| `SCHEDULE_TZ` |  | `Asia/Seoul` | Timezone of the automation |
| `FILTER_EXT` |  | `exe,msi,bat` | Extension list for the Type Filter |
| `FILTER_MODE` |  | `deny` | `allow` = only these pass · `deny` = these are blocked |
| `FILTER_MIN_MB` |  | — | Keep files at or above this size (MB) |
| `FILTER_MAX_MB` |  | — | Keep files at or below this size (MB); if both are set, the minimum is used |
| `SAVE_PATH` |  | `root` | `root` (no subfolder) · `date` (run-date subfolder) · `device` (subfolder per source device name) |
| `DUPLICATE_ACTION` |  | `overwrite` | `overwrite` · `rename` · `skip` when the target has a file with the same name |
| `INTEGRITY` |  | `true` | Verify each file after the transfer |

Every key, with comments: `.env.builder.example`.

### Understand Device Paths

`SOURCE_PATH` and `TARGET_PATH` refer to paths on the configured source and destination devices, respectively. They do **not** necessarily refer to folders on the computer running this Node.js program.

Confirm that the devices are registered, connected, and able to access the configured paths before starting a transfer.

### Review Integrations and File Options

Before execution, review any enabled options, including message notifications, virus scanning, email, monitoring, external API actions, file filters, save-path settings, duplicate-file handling, and integrity verification.

When an option such as `ON_MESSAGE` or `ON_VIRUS_SCAN` is enabled, the program may create or reuse a workspace-level integration. Provider-specific configuration may be required. Check the corresponding `*_CONFIG` value and consult the provider's requirements; the API endpoint `GET /api/integrations/rules/{type}` lists required integration fields.

Do not assume that `{}` is sufficient for an enabled integration. If required fields are missing, the program may report a warning and the integration may be saved without working correctly.

**Review these settings before running the program.** Depending on the configuration, it may create or reuse workspace-level integrations, create an automation, start an immediate operation, or set up a scheduled or externally triggered operation.

## 3. Configure Authentication

Configure **one** supported authentication method in `.env`:

| Configuration | Description |
|---|---|
| `INNORIX_API_KEY` | Long-lived user API key sent in the `x-api-key` request header. Recommended. |
| `INNORIX_ACCESS_TOKEN` | Short-lived JWT sent in the `Authorization: Bearer` header. |
| `INNORIX_EMAIL` and `INNORIX_PASSWORD` | Credentials used by the program to sign in and retrieve an access token. |

For most use cases, we recommend using `INNORIX_API_KEY`.

### Obtain an API Key

1. Sign in to the INNORIX Platform.
2. Open the profile menu at the bottom-left of the product interface.
3. Select **Developer**.
4. Copy the **API Key** and set it as `INNORIX_API_KEY` in `.env`.
5. Copy the **Workspace ID** shown in the same dialog and set `INNORIX_WORKSPACE_ID`.

Unless your environment requires another API endpoint, keep the base URL at its default value:

```dotenv
INNORIX_BASE_URL=https://app.innorix.com
```

An API key can also be created through `POST /api/auth/api-keys`. This request requires a Bearer access token and no request body; the generated key is returned as `data.apiKey`.

For more information, see the [INNORIX Developer Center](https://innorix.com/en/developers), [Quick Start](https://innorix.com/en/developers#irxApiQuick), and [API Reference](https://innorix.com/en/docs/api).

### Protect Credentials and Logs

**Never commit real credentials to Git.**

- Keep the local `.env` file out of version control. Verify that `.env` is excluded by `.gitignore`.
- Commit the `.env.builder.example` template, not a `.env` containing real keys, tokens, passwords, or provider secrets.
- Do not publish API keys or external trigger keys in screenshots, terminal output, tickets, or logs.
- If using `DEBUG=true`, review the program's debug behavior first. Debug output can include request and response details; do not share logs until you have checked them for sensitive information.
- An external trigger URL and its associated key should be treated as secrets because anyone with the required trigger information may be able to start the configured operation.

## 4. Run the Node.js Example

Open a terminal in the directory containing `combo_builder.js`. The script reads `.env` from that directory by default.

Run the program:

```bash
node combo_builder.js
```

To use a different configuration file, pass its path as the first argument:

```bash
node combo_builder.js path/to/other.env
```

Make sure the specified file exists and contains all settings required by the selected builder. If you provide a relative path, it is resolved from the current working directory; use an absolute path if you are unsure.

## 5. Verify the Result

After execution:

1. Review the terminal output for configuration errors, API errors, and the operation or transfer status.
2. Open the INNORIX Platform and check the relevant automation or transfer status.
3. For file transfers, confirm that the expected files arrived at the configured destination.
4. For scheduled, recurring, chained, or external-trigger scenarios, verify the configuration and execution status appropriate to that scenario rather than expecting an immediate file transfer.
5. If an integration is enabled, verify its own configuration and outcome as well as the transfer result.

The expected output depends on the example, its configuration, and the execution condition. Refer to the builder-specific README for scenario-specific expectations.

## 6. Troubleshooting

| Symptom | What to check first |
|---|---|
| `node` is not recognized or the script will not run | Install Node.js 18 or later and verify the installation with `node --version`. |
| The program cannot find `.env` | Run from the directory containing `combo_builder.js`, or supply a valid configuration file path. |
| The program cannot connect to the API | Check the base URL and network connection. If your environment requires an HTTP proxy, review the proxy instructions above. |
| Authentication fails | Check the selected authentication method, API key or token, Workspace ID, and base URL. Do not paste real credentials into support messages. |
| A device or path cannot be accessed | Confirm source and destination device IDs, device connectivity, paths, and agent access. Remember that paths belong to the configured devices. |
| An immediate transfer does not start | Review the required device/path settings in `.env` and the operation status in the Platform. |
| A scheduled or recurring operation does not run now | Check the configured time, timezone, recurrence settings, and automation status. Delayed execution may be expected. |
| An integration is created but does not work | Check the provider type and required fields in the corresponding `*_CONFIG` value. |
| The files transferred differ from expectations | Review file filters, source and destination paths, duplicate-file behavior, save-path settings, and integrity verification. |

For scenario-specific configuration, consult the selected builder's README and `.env.builder.example`.

## Documentation and Resources

- [INNORIX Platform](https://innorix.com/en/)
- [INNORIX Developer Center](https://innorix.com/en/developers)
- [Quick Start](https://innorix.com/en/developers#irxApiQuick)
- [Examples](https://innorix.com/en/docs/examples)
- [API Reference](https://innorix.com/en/docs/api)
- [Transfer Builder](https://innorix.com/en/developers#irxBuilder)
