# Collect Files from Many Devices When the Previous Transfer Ends With Follow-Up Actions

Result page: [innorix.com/en/builder/collect-many-after-transfer-follow-up](https://innorix.com/en/builder/collect-many-after-transfer-follow-up)

Gather files from multiple source devices into a collection destination when the preceding transfer ends, then run configured actions once collection completes. Both the start of collection and the work that follows it are driven by the same workflow.

| | This example |
| --- | --- |
| Devices | several source devices (`SOURCE_IDS`) → one target device |
| Starts | when the automation `PREV_AUTOMATION_ID` finishes (workflow chaining) |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
