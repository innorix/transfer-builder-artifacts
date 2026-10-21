# Collect Files from Many Devices Right Now With Follow-Up Actions

Result page: [innorix.com/en/builder/collect-many-now-follow-up](https://innorix.com/en/builder/collect-many-now-follow-up)

Gather files from several source devices into one collection destination right away, and execute additional tasks once the collection workflow completes.

| | This example |
| --- | --- |
| Devices | several source devices (`SOURCE_IDS`) → one target device |
| Starts | right now — the script waits for the run and prints the result |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
