# Send Files to One Device on a Recurring Schedule With Follow-Up Actions

Result page: [innorix.com/en/builder/send-one-recurring-follow-up](https://innorix.com/en/builder/send-one-recurring-follow-up)

Transfer files to one destination device on a recurring schedule, with follow-up actions configured to run after each completed transfer. Every run can therefore carry its own post-transfer processing.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
| Starts | on a recurring schedule (`REPEAT_EVERY` = hour · day · week · month) |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | follow-up actions: Run API, Monitoring, Message, Virus Scan, Email (`ON_*`) |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
