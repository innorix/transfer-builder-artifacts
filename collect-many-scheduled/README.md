# Collect Files from Many Devices at a Scheduled Time

Result page: [innorix.com/en/builder/collect-many-scheduled](https://innorix.com/en/builder/collect-many-scheduled)

Gather files from several source devices into one collection destination at a time you choose, so the collection runs without anyone starting it manually.

| | This example |
| --- | --- |
| Devices | several source devices (`SOURCE_IDS`) → one target device |
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
