# Send Matching Files to Many Devices on a Recurring Schedule

Result page: [innorix.com/en/builder/send-matching-many-recurring](https://innorix.com/en/builder/send-matching-many-recurring)

Repeatedly transfer the files that meet configured matching criteria to multiple destination devices on a recurring schedule. Each run works from the selection rules rather than a fixed file list, so what a run contains depends on which files match when it is evaluated.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
| Starts | on a recurring schedule (`REPEAT_EVERY` = hour · day · week · month) |
| Files | only files that match the Type / Size filter (`FILTER_*`) |
| After transfer | none |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
