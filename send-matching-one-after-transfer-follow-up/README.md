# Send Matching Files to One Device When the Previous Transfer Ends With Follow-Up Actions

Result page: [innorix.com/en/builder/send-matching-one-after-transfer-follow-up](https://innorix.com/en/builder/send-matching-one-after-transfer-follow-up)

Chain a selective transfer to one destination device to the completion of a preceding transfer, and run additional tasks once the new transfer finishes. Matching criteria, sequencing, and follow-up actions are configured together as one workflow.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
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
