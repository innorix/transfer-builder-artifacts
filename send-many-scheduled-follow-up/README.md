# Send Files to Many Devices at a Scheduled Time With Follow-Up Actions

Result page: [innorix.com/en/builder/send-many-scheduled-follow-up](https://innorix.com/en/builder/send-many-scheduled-follow-up)

Deliver files to multiple destination devices at a time you specify, and run additional tasks once the transfer completes. The schedule and the follow-up actions belong to the same workflow configuration.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
| Starts | once, at `SCHEDULE_AT` |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
