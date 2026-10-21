# Send Matching Files to One Device on a Recurring Schedule

Result page: [innorix.com/en/builder/send-matching-one-recurring](https://innorix.com/en/builder/send-matching-one-recurring)

Transfer only the files that satisfy the configured selection criteria to one destination device at each run of a recurring schedule. Each run is intended to carry the matching subset rather than the whole source location.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
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
