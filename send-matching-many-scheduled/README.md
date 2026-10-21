# Send Matching Files to Many Devices at a Scheduled Time

Result page: [innorix.com/en/builder/send-matching-many-scheduled](https://innorix.com/en/builder/send-matching-many-scheduled)

Select the files that meet configured matching criteria on a single source device and transfer them to multiple destination devices at a scheduled time. The selection rules decide which files a run carries, so only matching files are distributed.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
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
