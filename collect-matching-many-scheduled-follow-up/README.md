# Collect Matching Files from Many Devices at a Scheduled Time With Follow-Up Actions

Result page: [innorix.com/en/builder/collect-matching-many-scheduled-follow-up](https://innorix.com/en/builder/collect-matching-many-scheduled-follow-up)

Gather only the files matching your selection criteria from several source devices into one collection destination at a planned time, then execute additional tasks after the collection workflow completes.

| | This example |
| --- | --- |
| Devices | several source devices (`SOURCE_IDS`) → one target device |
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
