# Send Matching Files to One Device When the Previous Transfer Ends

Result page: [innorix.com/en/builder/send-matching-one-after-transfer](https://innorix.com/en/builder/send-matching-one-after-transfer)

Start a selective transfer to one destination device when the previous transfer ends, moving only the files that meet the configured selection criteria. Both the sequencing trigger and the matching rules belong to the same workflow configuration.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
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
