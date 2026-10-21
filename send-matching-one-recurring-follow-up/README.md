# Send Matching Files to One Device on a Recurring Schedule With Follow-Up Actions

Result page: [innorix.com/en/builder/send-matching-one-recurring-follow-up](https://innorix.com/en/builder/send-matching-one-recurring-follow-up)

Transfer the files that match the configured criteria to one destination device on a recurring schedule, and run the configured follow-up actions after each transfer. One definition covers the rule, the recurrence, and the work that follows.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
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
