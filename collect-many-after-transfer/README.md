# Collect Files from Many Devices When the Previous Transfer Ends

Result page: [innorix.com/en/builder/collect-many-after-transfer](https://innorix.com/en/builder/collect-many-after-transfer)

Gather files from multiple source devices into one collection destination, with collection starting when the preceding transfer ends. The workflow chains collection to a completion-based trigger rather than a manual start.

| | This example |
| --- | --- |
| Devices | several source devices (`SOURCE_IDS`) → one target device |
| Starts | when the automation `PREV_AUTOMATION_ID` finishes (workflow chaining) |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | none |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
