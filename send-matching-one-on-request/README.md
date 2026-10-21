# Send Matching Files to One Device When an External Request Arrives

Result page: [innorix.com/en/builder/send-matching-one-on-request](https://innorix.com/en/builder/send-matching-one-on-request)

Use an external request to start a selective transfer to one destination device, moving only the files that meet the configured selection criteria. Request handling and matching rules are both defined in the workflow configuration.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
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
