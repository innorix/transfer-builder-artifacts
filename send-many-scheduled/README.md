# Send Files to Many Devices at a Scheduled Time

Result page: [innorix.com/en/builder/send-many-scheduled](https://innorix.com/en/builder/send-many-scheduled)

Deliver files from a single source device to multiple destination devices at a time you specify, rather than as soon as the workflow is configured. The execution time is set as part of the transfer configuration.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
| Starts | once, at `SCHEDULE_AT` |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | none |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
