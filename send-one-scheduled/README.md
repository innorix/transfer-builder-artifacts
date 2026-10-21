# Send Files to One Device at a Scheduled Time

Result page: [innorix.com/en/builder/send-one-scheduled](https://innorix.com/en/builder/send-one-scheduled)

Transfer files from a single source device to one destination device at a specified time rather than on demand. The schedule is part of the workflow, so the delivery is planned in advance instead of being started manually.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
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
