# Send Files to Many Devices Right Now With Follow-Up Actions

Result page: [innorix.com/en/builder/send-many-now-follow-up](https://innorix.com/en/builder/send-many-now-follow-up)

Transfer files from a single source device to multiple destination devices right away, and execute additional tasks after the transfer completes.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
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
