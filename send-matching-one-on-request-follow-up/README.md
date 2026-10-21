# Send Matching Files to One Device When an External Request Arrives With Follow-Up Actions

Result page: [innorix.com/en/builder/send-matching-one-on-request-follow-up](https://innorix.com/en/builder/send-matching-one-on-request-follow-up)

Combine an external request trigger, configured file selection criteria, and post-transfer tasks in a workflow aimed at one destination device. The request starts the selective transfer, and the follow-up actions are configured to run once it completes.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
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
