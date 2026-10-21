# Send Matching Files to Many Devices When an External Request Arrives With Follow-Up Actions

Result page: [innorix.com/en/builder/send-matching-many-on-request-follow-up](https://innorix.com/en/builder/send-matching-many-on-request-follow-up)

Send only the files that meet your selection criteria to multiple destination devices when an external request triggers the workflow, then execute additional tasks once the transfer completes.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
| Starts | when an external system calls the trigger URL the script prints |
| Files | only files that match the Type / Size filter (`FILTER_*`) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
