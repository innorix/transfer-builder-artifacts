# Send Files to Many Devices When an External Request Arrives With Follow-Up Actions

Result page: [innorix.com/en/builder/send-many-on-request-follow-up](https://innorix.com/en/builder/send-many-on-request-follow-up)

Let an external request initiate a transfer from a single source device to multiple destination devices, and perform additional tasks after the transfer completes. The caller starts the distribution and the configured follow-up actions cover what belongs after it.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
| Starts | when an external system calls the trigger URL the script prints |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
