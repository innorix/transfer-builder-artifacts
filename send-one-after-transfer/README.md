# Send Files to One Device When the Previous Transfer Ends

Result page: [innorix.com/en/builder/send-one-after-transfer](https://innorix.com/en/builder/send-one-after-transfer)

Start a transfer to one destination device when the previous transfer ends, linking the two steps through a completion trigger instead of a clock time. The second delivery is defined with its own destination and files.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
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
