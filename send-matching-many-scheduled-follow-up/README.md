# Send Matching Files to Many Devices at a Scheduled Time With Follow-Up Actions

Result page: [innorix.com/en/builder/send-matching-many-scheduled-follow-up](https://innorix.com/en/builder/send-matching-many-scheduled-follow-up)

Transfer only the files that meet configured matching criteria to multiple destination devices at a scheduled time, then perform additional tasks once the transfer completes. The schedule governs when the run starts, the selection rules govern what it carries, and the follow-up actions cover what happens afterwards.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
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
