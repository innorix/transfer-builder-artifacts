# Collect Matching Files from Many Devices When the Previous Transfer Ends With Follow-Up Actions

Result page: [innorix.com/en/builder/collect-matching-many-after-transfer-follow-up](https://innorix.com/en/builder/collect-matching-many-after-transfer-follow-up)

Collect files matching your selection rule from multiple source devices when the preceding transfer ends, and run configured actions once collection completes. Filtering, sequencing, and post-collection work are described by a single workflow.

| | This example |
| --- | --- |
| Devices | several source devices (`SOURCE_IDS`) → one target device |
| Starts | when the automation `PREV_AUTOMATION_ID` finishes (workflow chaining) |
| Files | only files that match the Type / Size filter (`FILTER_*`) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
