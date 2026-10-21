# Send Files to One Device Right Now

Result page: [innorix.com/en/builder/send-one-now](https://innorix.com/en/builder/send-one-now)

Transfer files from a single source device to one destination device as soon as the workflow runs. The configuration covers only the source, the destination, and the files, with no schedule and no additional steps after completion.

| | This example |
| --- | --- |
| Devices | one source device → one target device |
| Starts | right now — the script waits for the run and prints the result |
| Files | all files under `SOURCE_PATH` (no filter) |
| After transfer | none |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
