# Collect Matching Files from Many Devices When an External Request Arrives

Result page: [innorix.com/en/builder/collect-matching-many-on-request](https://innorix.com/en/builder/collect-matching-many-on-request)

Use an external request to gather only the files matching your selection rule from multiple source devices. Request-driven execution is combined with file selection, so each run brings back a filtered set rather than everything a device holds.

| | This example |
| --- | --- |
| Devices | several source devices (`SOURCE_IDS`) → one target device |
| Starts | when an external system calls the trigger URL the script prints |
| Files | only files that match the Type / Size filter (`FILTER_*`) |
| After transfer | none |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
