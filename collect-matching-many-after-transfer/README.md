# Collect Matching Files from Many Devices When the Previous Transfer Ends

Result page: [innorix.com/en/builder/collect-matching-many-after-transfer](https://innorix.com/en/builder/collect-matching-many-after-transfer)

Collect only the files that meet your selection rule from multiple source devices, starting when the preceding transfer ends. Matching criteria narrow what is gathered, so the destination receives a filtered set rather than everything available on each device.

| | This example |
| --- | --- |
| Devices | several source devices (`SOURCE_IDS`) → one target device |
| Starts | when the automation `PREV_AUTOMATION_ID` finishes (workflow chaining) |
| Files | only files that match the Type / Size filter (`FILTER_*`) |
| After transfer | none |

Pick one language. Each folder is self-contained: the script, `.env.builder.example` (+ `.env`) and a README.

| Language | Folder |
| --- | --- |
| Node.js | [`nodejs/combo_builder.js`](nodejs/) |
| Python | [`python/combo_builder.py`](python/) |
| Java | [`java/ComboBuilder.java`](java/) |
| C# | [`csharp/ComboBuilder.cs`](csharp/) |
