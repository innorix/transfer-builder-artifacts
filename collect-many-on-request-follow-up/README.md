# Collect Files from Many Devices When an External Request Arrives With Follow-Up Actions

Result page: [innorix.com/en/builder/collect-many-on-request-follow-up](https://innorix.com/en/builder/collect-many-on-request-follow-up)

Accept an external request to gather files from multiple source devices, and run configured actions once collection completes. The request starts the collection, and the workflow carries it through the steps that follow.

| | This example |
| --- | --- |
| Devices | several source devices (`SOURCE_IDS`) → one target device |
| Starts | when an external system calls the trigger URL the script prints |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
