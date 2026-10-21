# Send Files to One Device Right Now With Follow-Up Actions

Result page: [innorix.com/en/builder/send-one-now-follow-up](https://innorix.com/en/builder/send-one-now-follow-up)

Transfer files from a single source device to one destination device right away, then run the additional tasks configured to follow transfer completion. Delivery and post-transfer processing are described in the same workflow.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
| Starts | right now — the script waits for the run and prints the result |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
