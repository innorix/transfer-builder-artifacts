# Send Matching Files to Many Devices Right Now With Follow-Up Actions

Result page: [innorix.com/en/builder/send-matching-many-now-follow-up](https://innorix.com/en/builder/send-matching-many-now-follow-up)

Distribute only the files that meet the configured selection criteria to multiple destination devices right away, then run additional tasks once the transfer completes. Matching rules and follow-up actions are configured together.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
| Starts | right now — the script waits for the run and prints the result |
| Files | only files that match the Type / Size filter (`FILTER_*`) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
