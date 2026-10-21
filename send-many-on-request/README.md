# Send Files to Many Devices When an External Request Arrives

Result page: [innorix.com/en/builder/send-many-on-request](https://innorix.com/en/builder/send-many-on-request)

Start a distribution to multiple destination devices in response to an external request made through a supported integration such as an API call. The request is the trigger, so the fan-out happens when another system asks for it rather than on a timer.

| | This example |
| --- | --- |
| Devices | one source device → several target devices (`TARGET_IDS`) |
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
