# Send Files to One Device on a Recurring Schedule

Result page: [innorix.com/en/builder/send-one-recurring](https://innorix.com/en/builder/send-one-recurring)

Transfer files from a single source device to one destination device repeatedly, following a recurring schedule instead of a single planned time. The same delivery is set up once and runs again at each configured interval.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
| Starts | on a recurring schedule (`REPEAT_EVERY` = hour · day · week · month) |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | none |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
