# Send Files to Many Devices When the Previous Transfer Ends

Result page: [innorix.com/en/builder/send-many-after-transfer](https://innorix.com/en/builder/send-many-after-transfer)

Start a distribution to multiple destination devices once a preceding transfer has ended, so the two transfers are arranged in sequence rather than side by side. The end of the earlier transfer is the configured trigger for this one.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
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
