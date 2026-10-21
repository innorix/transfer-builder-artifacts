# Send Files to One Device at a Scheduled Time With Follow-Up Actions

Result page: [innorix.com/en/builder/send-one-scheduled-follow-up](https://innorix.com/en/builder/send-one-scheduled-follow-up)

Transfer files to one destination device at a specified time, and run the configured follow-up actions once that scheduled transfer completes. The timing and the post-transfer tasks are configured together.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
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
