# Collect Files from Many Devices When an External Request Arrives

Result page: [innorix.com/en/builder/collect-many-on-request](https://innorix.com/en/builder/collect-many-on-request)

Gather files from multiple source devices into a collection destination when an external request arrives through the supported integration. Collection runs on demand instead of following a fixed schedule or an earlier transfer.

| | This example |
| --- | --- |
| Devices | several source devices (`SOURCE_IDS`) → one target device |
| Starts | when an external system calls the trigger URL the script prints |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | none |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
