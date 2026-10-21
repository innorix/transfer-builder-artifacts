# Send Files to Many Devices When the Previous Transfer Ends With Follow-Up Actions

Result page: [innorix.com/en/builder/send-many-after-transfer-follow-up](https://innorix.com/en/builder/send-many-after-transfer-follow-up)

Trigger a distribution to multiple destination devices when a preceding transfer ends, then perform additional tasks after this transfer completes. The workflow places two transfers in sequence and adds the steps that belong after the second one.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
| Starts | when the automation `PREV_AUTOMATION_ID` finishes (workflow chaining) |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
