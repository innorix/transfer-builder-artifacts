# Send Matching Files to Many Devices When the Previous Transfer Ends With Follow-Up Actions

Result page: [innorix.com/en/builder/send-matching-many-after-transfer-follow-up](https://innorix.com/en/builder/send-matching-many-after-transfer-follow-up)

Chain a filtered distribution to multiple destination devices to the end of a preceding transfer, and perform additional tasks once this transfer completes. Matching criteria, the completion trigger, and the post-transfer actions make up a single sequenced workflow.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
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
