# Send Matching Files to Many Devices When the Previous Transfer Ends

Result page: [innorix.com/en/builder/send-matching-many-after-transfer](https://innorix.com/en/builder/send-matching-many-after-transfer)

When a preceding transfer ends, transfer the files that meet configured matching criteria to multiple destination devices. The trigger governs when the run starts and the selection rules govern which of the available files it carries.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
| Starts | when the automation `PREV_AUTOMATION_ID` finishes (workflow chaining) |
| Files | only files that match the Type / Size filter (`FILTER_*`) |
| After transfer | none |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
