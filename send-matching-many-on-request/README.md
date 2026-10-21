# Send Matching Files to Many Devices When an External Request Arrives

Result page: [innorix.com/en/builder/send-matching-many-on-request](https://innorix.com/en/builder/send-matching-many-on-request)

Use an external request through a supported integration to start a filtered distribution to multiple destination devices. The configured matching criteria determine which of the available files a triggered run carries.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
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
