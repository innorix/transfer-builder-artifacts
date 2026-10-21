# Send Files to One Device When an External Request Arrives With Follow-Up Actions

Result page: [innorix.com/en/builder/send-one-on-request-follow-up](https://innorix.com/en/builder/send-one-on-request-follow-up)

Start a transfer to one destination device when an external request arrives, and run additional tasks once the transfer finishes. The request integration and the follow-up actions are part of the same workflow configuration.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
| Starts | when an external system calls the trigger URL the script prints |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
