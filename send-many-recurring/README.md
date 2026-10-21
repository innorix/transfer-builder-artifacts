# Send Files to Many Devices on a Recurring Schedule

Result page: [innorix.com/en/builder/send-many-recurring](https://innorix.com/en/builder/send-many-recurring)

Transfer files from a single source device to multiple destination devices repeatedly on a recurring schedule. No file selection rule is applied, so each run is defined by the configured files and destinations rather than by a filter.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
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
