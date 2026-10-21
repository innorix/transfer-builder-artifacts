# INNORIX Transfer Builder — Java Guide

This guide explains how to configure and run the Java implementation of an INNORIX Transfer Builder example.

`ComboBuilder.java` runs the transfer configured in the INNORIX **Transfer Builder**. The program reads its settings from a `.env` file, so you can adjust the transfer configuration without editing the Java source code.

Each builder example has its own `README.md` describing the transfer scenario and its required settings. Use this guide for the Java runtime, configuration-file preparation, authentication, execution, and common troubleshooting steps.

## Before You Start

Make sure you have the following before running the example:

- An INNORIX Platform account.
- An API key or another supported authentication method.
- The Workspace ID for the workspace you will use.
- Registered and connected source and destination devices, as required by your selected transfer scenario.
- The files for the selected builder's Java directory.

New to INNORIX? Start with the [repository README](../../README.md) for the overall setup process. For the selected transfer scenario and its required configuration, read the builder-specific [README](../README.md).

## Requirements

- **Java 11 or later**
- A terminal or command-line interface
- Access to the selected builder's Java directory

The program uses Java's built-in `java.net.http` HTTP client and includes its own lightweight JSON reader/writer, so no external Java dependencies are required.

Verify your installed Java version:

```bash
java -version
```

If you plan to compile the source before running it, also verify that the Java compiler is available:

```bash
javac -version
```

## 1. Understand the Files

The selected Java directory typically contains these files:

| File | Purpose |
|---|---|
| `ComboBuilder.java` | Java program that submits and monitors the configured operation. |
| `.env` | Builder-generated configuration for the selected scenario. Authentication values must be supplied by you. |
| `.env.builder.example` | General configuration template documenting the supported settings and their meanings. |
| `README.md` | Java execution guide for this example. |

### Keep the generated `.env`

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

Builder page: [collect-matching-many-on-request-follow-up](https://innorix.com/en/builder/collect-matching-many-on-request-follow-up) — several source devices → one target device, runs when an external system calls the trigger URL the program prints; only files that match the filter are sent; follow-up actions run around the transfer.

The transfer type and start condition are fixed in this example's code, so `TRANSFER_TYPE` and `START_WHEN` are not used. Set these keys in `.env`:

| Key | Required | Value in `.env` | Description |
| --- | --- | --- | --- |
| `INNORIX_API_KEY` | ✔ (or token / login) | — | User API key, sent as `x-api-key` (or set `INNORIX_ACCESS_TOKEN`, or `INNORIX_EMAIL` + `INNORIX_PASSWORD`) |
| `INNORIX_WORKSPACE_ID` |  | — | Workspace to act in |
| `SOURCE_IDS` | ✔ | `<deviceId>,<deviceId>,<deviceId>` | Source device IDs, comma separated |
| `SOURCE_PATHS` | ✔ | `C:/data/out,/data/out,/data/out` | One path for every source, or one per source in the same order |
| `TARGET_ID` | ✔ | `<deviceId>` | Target device ID |
| `TARGET_PATH` | ✔ | `C:/collect` | Path on the target device |
| `SOURCE_IS_DIR` |  | `true` | `false` when the source path is a single file |
| `NAME` |  | `collect-matching-many-on-request-follow-up` | Automation name |
| `FIRE_TRIGGER` |  | `false` | `true` calls the trigger URL once right after creating it |
| `SCHEDULE_TZ` |  | `Asia/Seoul` | Timezone of the automation |
| `FILTER_EXT` |  | `exe,msi,bat` | Extension list for the Type Filter |
| `FILTER_MODE` |  | `deny` | `allow` = only these pass · `deny` = these are blocked |
| `FILTER_MIN_MB` |  | — | Keep files at or above this size (MB) |
| `FILTER_MAX_MB` |  | — | Keep files at or below this size (MB); if both are set, the minimum is used |
| `SAVE_PATH` |  | `root` | `root` (no subfolder) · `date` (run-date subfolder) · `device` (subfolder per source device name) |
| `DUPLICATE_ACTION` |  | `overwrite` | `overwrite` · `rename` · `skip` when the target has a file with the same name |
| `INTEGRITY` |  | `true` | Verify each file after the transfer |
| `NOTIFY_EVENTS` |  | `completed,error` | Events that fire Message / Virus Scan / Email / Monitoring |
| `ON_MESSAGE` |  | `true` | Chat notification (workspace-wide integration) |
| `MESSAGE_TYPE` | ✔ (when `ON_MESSAGE=true`) | `slack` | Provider: `slack`, `microsoft_teams`, `discord`, `telegram`, … |
| `MESSAGE_NAME` |  | `builder-message-slack` | Integration name |
| `MESSAGE_EVENTS` |  | `started,completed,error` | Events for the message only (blank = `NOTIFY_EVENTS`) |
| `MESSAGE_CONFIG` | ✔ (when `ON_MESSAGE=true`) | `{"webhookUrl":"https://hooks.slack.com/services/XXX","channel":"#transfers"}` | Provider fields as JSON — Slack needs `webhookUrl` (and `channel`) |
| `ON_VIRUS_SCAN` |  | `true` | Virus-scan integration (workspace-wide) |
| `VIRUS_SCAN_TYPE` | ✔ (when `ON_VIRUS_SCAN=true`) | `microsoft_defender` | Provider — only `microsoft_defender` scans in the transfer pipeline |
| `VIRUS_SCAN_NAME` |  | `builder-virus-scan-defender` | Integration name |
| `VIRUS_SCAN_CONFIG` | ✔ (when `ON_VIRUS_SCAN=true`) | `{"scanTiming":"before_transfer_source","onDetection":"block_transfer","scanScope":"all"}` | JSON with `scanTiming`, `onDetection`, `scanScope` (all required) |
| `ON_EMAIL` |  | `false` | Email integration (workspace-wide) |
| `EMAIL_TYPE` |  | `ses` | `ses` · `sendgrid` · `email` |
| `EMAIL_NAME` |  | `builder-email` | Integration name |
| `EMAIL_CONFIG` |  | `{}` | Provider fields as JSON |
| `ON_MONITORING` |  | `false` | Monitoring processor attached to this automation |
| `MONITORING_TYPE` |  | `grafana` | `grafana` · `datadog` · `prometheus` · … |
| `MONITORING_NAME` |  | `builder-grafana` | Integration name |
| `MONITORING_CONFIG` |  | `{"baseUrl":"https://grafana.company.com","apiToken":"XXX"}` | Provider fields as JSON |
| `ON_RUN_API` |  | `false` | Run API action attached to this automation |
| `RUN_API_TYPE` |  | `http` | `http` (call a URL) · `command` (shell command) · `script` (script file) |
| `RUN_API_URL` |  | `https://api.example.com/webhook` | URL to call (`RUN_API_TYPE=http`) |
| `RUN_API_METHOD` |  | `POST` | HTTP method |
| `RUN_API_BODY` |  | — | Optional JSON body |
| `RUN_API_EVENTS` |  | `completed` | Transfer events that fire it (blank = every event) |
| `RUN_API_DEVICE` |  | — | Which device runs it (blank = the transfer's device) |

Every key, with comments: `.env.builder.example`.

### Understand Device Paths

`SOURCE_PATH` and `TARGET_PATH` refer to paths on the configured source and destination devices, respectively. They do **not** necessarily refer to folders on the computer running this Java program.

Confirm that the devices are registered, connected, and able to access the configured paths before starting a transfer.

### Review Integrations and File Options

Before execution, review any enabled options, including message notifications, virus scanning, email, monitoring, external API actions, file filters, save-path settings, duplicate-file handling, and integrity verification.

When an option such as `ON_MESSAGE` or `ON_VIRUS_SCAN` is enabled, the program may create or reuse a workspace-level integration. Provider-specific configuration may be required. Check the corresponding `*_CONFIG` value and consult the provider's requirements; the API endpoint `GET /api/integrations/rules/{type}` lists required integration fields.

Do not assume that `{}` is sufficient for an enabled integration. If required fields are missing, the program may report a warning and the integration may be saved without working correctly.

## 3. Configure Authentication

Configure **one** supported authentication method in `.env`:

| Configuration | Description |
|---|---|
| `INNORIX_API_KEY` | Long-lived user API key sent in the `x-api-key` request header. Recommended. |
| `INNORIX_ACCESS_TOKEN` | Short-lived JWT sent in the `Authorization: Bearer` header. |
| `INNORIX_EMAIL` and `INNORIX_PASSWORD` | Credentials used by the program to sign in and obtain an access token. |

For most use cases, use `INNORIX_API_KEY`.

### Obtain an API Key

1. Sign in to the INNORIX Platform.
2. Open the profile menu at the bottom-left of the product interface.
3. Select **Developer**.
4. Copy the **API Key** into `.env` as `INNORIX_API_KEY`.

The same dialog displays the Workspace ID. Set that value as `INNORIX_WORKSPACE_ID` in `.env`.

Unless your environment requires a different endpoint, keep the base URL at its default value:

```dotenv
INNORIX_BASE_URL=https://app.innorix.com
```

An API key can also be created through `POST /api/auth/api-keys`. This request requires a Bearer access token and no request body. The generated key is returned as `data.apiKey`.

For more information, visit the [INNORIX Developer Center](https://innorix.com/en/developers).

### Protect Credentials and Logs

**Never commit real credentials or secret configuration files to Git.**

- Keep your real `.env` local and ensure `.env` is excluded by `.gitignore`.
- Track `.env.builder.example` as the configuration template, not your populated `.env`.
- Do not share API keys, access tokens, passwords, webhook URLs, or external-trigger credentials.
- If `DEBUG=true`, the program prints request and response details. Review logs before sharing them, as response bodies or operational details may contain sensitive information.
- For examples started by an external request, treat the printed trigger `x-api-key` as a secret. Anyone who obtains it may be able to trigger the configured operation.

## 4. Run the Java Example

Open a terminal in the directory containing `ComboBuilder.java`.

### Option A: Run the source file directly

Java 11 or later supports launching a single source file directly:

```bash
java ComboBuilder.java
```

The program reads `.env` from the current working directory when that file exists. If it is not present there, the program falls back to looking for `.env` beside the program's code location.

To use a different configuration file, pass its path as the first argument:

```bash
java ComboBuilder.java path/to/other.env
```

### Option B: Compile, then run

You can also compile the source file first. Run these commands in order:

```bash
javac -encoding UTF-8 ComboBuilder.java
java ComboBuilder
```

On Windows PowerShell, run the same commands on separate lines:

```powershell
javac -encoding UTF-8 ComboBuilder.java
java ComboBuilder
```

Always pass `-encoding UTF-8`. The source file is UTF-8, and on JDK 11 to 17 `javac` otherwise uses the system locale (for example Korean, Japanese or Chinese Windows) and stops with `unmappable character for encoding` errors. JDK 18 and later default to UTF-8, where the option is harmless.

To use a different configuration file with the compiled program:

```bash
java ComboBuilder path/to/other.env
```

Run the commands from the Java example directory when possible so the expected `.env` file is easy to locate.

## 5. Verify the Result

After running the program:

1. Review the terminal output for the selected transfer type and start condition.
2. For an immediate run, wait for the final transfer status if `WAIT=true`.
3. Open the INNORIX Platform and check the operation or automation status.
4. For file transfers, confirm that the expected files arrived at the configured destination.
5. For scheduled, recurring, chained, or external-trigger operations, verify the created automation and its settings. These modes may not transfer files immediately.

The expected result depends on the selected builder and the settings in `.env`.

## 6. Troubleshooting

| Symptom | What to check |
|---|---|
| `java` or `javac` is not recognized | Install a Java 11+ JDK and confirm that its `bin` directory is on `PATH`. |
| `error: unmappable character (0x…) for encoding …` | Compile with `javac -encoding UTF-8 ComboBuilder.java`, or run the source directly with `java ComboBuilder.java`. |
| The `.env` file is not found | Run the command from the Java example directory, check the file name, or provide the correct path as the first argument. |
| Authentication fails | Check the selected authentication method, API key or token, and Workspace ID. |
| The source or destination cannot be accessed | Verify device IDs, paths on the registered devices, and device connection status. |
| No transfer starts | Review the required settings in `.env` and whether this example waits for a schedule or an external trigger. |
| An integration is enabled but does not work | Check the provider type and its required fields in the corresponding `*_CONFIG` value. |
| The transfer result is unexpected | Review file filters, save-path settings, duplicate-file handling, integrity options, and the operation status. |
| A transfer takes too long | Check the destination device/agent connection and review `WAIT_TIMEOUT` and `APPEAR_WAIT` where applicable. |

For scenario-specific settings, consult the `.env.builder.example` file and the README in your selected builder directory.

## Documentation and Resources

- [INNORIX Platform](https://innorix.com/en/)
- [INNORIX Developer Center](https://innorix.com/en/developers)
- [Quick Start](https://innorix.com/en/developers#irxApiQuick)
- [Examples](https://innorix.com/en/docs/examples)
- [API Reference](https://innorix.com/en/docs/api)
- [Transfer Builder](https://innorix.com/en/developers#irxBuilder)
