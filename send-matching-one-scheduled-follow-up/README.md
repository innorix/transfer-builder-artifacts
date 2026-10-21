# Send Matching Files to One Device at a Scheduled Time With Follow-Up Actions

Result page: [innorix.com/en/builder/send-matching-one-scheduled-follow-up](https://innorix.com/en/builder/send-matching-one-scheduled-follow-up)

Transfer the files matching the configured criteria to one destination device at a planned time, then run the follow-up actions set for after completion. Selection, timing, and post-transfer processing sit in one workflow.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
| Starts | once, at `SCHEDULE_AT` |
| Files | only files that match the Type / Size filter (`FILTER_*`) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
