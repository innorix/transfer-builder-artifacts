# Send Matching Files to Many Devices on a Recurring Schedule With Follow-Up Actions

Result page: [innorix.com/en/builder/send-matching-many-recurring-follow-up](https://innorix.com/en/builder/send-matching-many-recurring-follow-up)

Run a recurring distribution that carries only the files meeting configured matching criteria to multiple destination devices, and perform additional tasks after the transfer completes. Selection rules, recurrence, and post-transfer actions are configured together as one workflow.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
| Starts | on a recurring schedule (`REPEAT_EVERY` = hour · day · week · month) |
| Files | only files that match the Type / Size filter (`FILTER_*`) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
