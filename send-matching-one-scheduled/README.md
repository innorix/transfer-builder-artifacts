# Send Matching Files to One Device at a Scheduled Time

Result page: [innorix.com/en/builder/send-matching-one-scheduled](https://innorix.com/en/builder/send-matching-one-scheduled)

Transfer only the files that meet the configured selection criteria to one destination device at a specified time. The rule decides what is sent and the schedule decides when the run takes place.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
| Starts | once, at `SCHEDULE_AT` |
| Files | only files that match the Type / Size filter (`FILTER_*`) |
| After transfer | none |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
