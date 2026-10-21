# Send Files to One Device When an External Request Arrives

Result page: [innorix.com/en/builder/send-one-on-request](https://innorix.com/en/builder/send-one-on-request)

Start a transfer to one destination device in response to an external request raised by your own application through the supported integration mechanism. The request interface, the destination, and the transfer behavior are all defined in configuration.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
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
