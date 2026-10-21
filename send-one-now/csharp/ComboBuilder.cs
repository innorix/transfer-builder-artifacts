// =============================================================================
//  INNORIX Transfer Builder example — Send Files to One Device Right Now
// -----------------------------------------------------------------------------
//  Result page : https://innorix.com/en/builder/send-one-now
//  Devices     : one source device -> one target device
//  Starts      : right now (the program waits for the run and prints the result)
//
//  Every setting lives in the .env file next to this program (see .env.builder.example).
//  Requirements:  .NET 8+ (standard library only, no external dependencies)
//  Run:
//    dotnet run                        # use the .env in this folder
//    dotnet run -- path/to/x.env       # or point it at another .env
//  (Keep it in the same folder as combo_builder.csproj.)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

using Obj = System.Collections.Generic.Dictionary<string, object?>;
using Arr = System.Collections.Generic.List<object?>;

static class ComboBuilder
{
    // ============================================================== .env & env
    static readonly Dictionary<string, string> ENV = new();

    static void LoadEnvFile(string file)
    {
        if (!File.Exists(file)) { Console.WriteLine($"note: {file} not found, using the current environment only"); return; }
        foreach (var raw in File.ReadAllLines(file, Encoding.UTF8))
        {
            var line = raw.Trim();
            int eq = line.IndexOf('=');
            if (line.Length == 0 || line.StartsWith("#") || eq < 0) continue;
            var key = line.Substring(0, eq).Trim();
            var value = line.Substring(eq + 1).Trim();
            char c0 = value.Length == 0 ? ' ' : value[0];
            if (c0 != '"' && c0 != '\'')
            {
                if (value.StartsWith("#")) value = "";
                else { int h = value.IndexOf(" #", StringComparison.Ordinal); if (h >= 0) value = value.Substring(0, h).TrimEnd(); }
            }
            else if (value.Length >= 2 && value[value.Length - 1] == c0) value = value.Substring(1, value.Length - 2);
            if (!ENV.ContainsKey(key) && Environment.GetEnvironmentVariable(key) == null) ENV[key] = value;
        }
    }

    static string? Env(string key, string? dflt = null)
    {
        var v = Environment.GetEnvironmentVariable(key) ?? (ENV.TryGetValue(key, out var e) ? e : null);
        return string.IsNullOrWhiteSpace(v) ? dflt : v.Trim();
    }
    static bool EnvBool(string key, bool dflt = false)
    {
        var v = Env(key);
        return v == null ? dflt : new[] { "1", "true", "yes", "on" }.Contains(v.ToLowerInvariant());
    }
    static int EnvInt(string key, int dflt)
    {
        var v = Env(key);
        if (v == null) return dflt;
        if (!int.TryParse(v, out var n)) Fail($"{key} must be an integer, got: {v}");
        return int.Parse(v);
    }
    static string Require(string key)
    {
        var v = Env(key);
        if (v == null) Fail($"missing required setting in .env: {key}");
        return v!;
    }
    static void Fail(string msg) { Console.Error.WriteLine(msg); Environment.Exit(1); }

    static Obj Ob(params object?[] kv)
    {
        var m = new Obj();
        for (int i = 0; i < kv.Length; i += 2) m[(string)kv[i]!] = kv[i + 1];
        return m;
    }
    static Arr Ar(params object?[] items) => new Arr(items);

    static readonly JsonSerializerOptions JSON = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never };
    static string ToJson(object? o) => JsonSerializer.Serialize(o, JSON);

    // ============================================================== API client
    sealed class ApiError : Exception { public readonly int Status; public ApiError(int status, string msg) : base(msg) { Status = status; } }

    sealed class Api
    {
        public readonly string Base; public readonly int Timeout; public readonly string? Workspace;
        public readonly bool Debug;
        public string? ApiKey; public string? Access; string? _refresh;
        readonly HttpClient _http = new();

        public Api()
        {
            Base = (Env("INNORIX_BASE_URL", "https://app.innorix.com")!).TrimEnd('/');
            Timeout = EnvInt("INNORIX_TIMEOUT", 30);
            Workspace = Env("INNORIX_WORKSPACE_ID");
            Debug = EnvBool("DEBUG");   // DEBUG=true -> print every request + response
            ApiKey = Env("INNORIX_API_KEY");        // -> x-api-key
            Access = Env("INNORIX_ACCESS_TOKEN");   // -> Authorization: Bearer
            _http.Timeout = TimeSpan.FromSeconds(Timeout);
            if (ApiKey == null && Access == null)
            {
                var email = Env("INNORIX_EMAIL"); var password = Env("INNORIX_PASSWORD");
                if (email == null || password == null)
                    Fail("set INNORIX_API_KEY or INNORIX_ACCESS_TOKEN, or INNORIX_EMAIL and INNORIX_PASSWORD in .env");
                Login(email!, password!);
            }
        }

        public (int status, string body) Send(string method, string url, Dictionary<string, string> headers, object? body)
        {
            try
            {
                var req = new HttpRequestMessage(new HttpMethod(method), url);
                if (body != null) req.Content = new StringContent(ToJson(body), Encoding.UTF8, "application/json");
                foreach (var h in headers) if (!h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) req.Headers.TryAddWithoutValidation(h.Key, h.Value);
                using var resp = _http.Send(req);
                using var reader = new StreamReader(resp.Content.ReadAsStream());
                return ((int)resp.StatusCode, reader.ReadToEnd());
            }
            catch (Exception e) { throw new ApiError(0, $"cannot reach {Base}: {e.GetType().Name}"); }
        }

        void Login(string email, string password)
        {
            var (st, bd) = Send("POST", Base + "/api/auth/login", new(), Ob("email", email, "password", password));
            if (st / 100 != 2) Fail($"login failed ({st}): {Snippet(bd, 200)}");
            var user = JsonNode.Parse(bd)?["data"]?["user"];
            Access = user?["accessToken"]?.GetValue<string>();
            _refresh = user?["refreshToken"]?.GetValue<string>();
        }

        bool Renew()
        {
            if (_refresh == null) return false;
            var (st, bd) = Send("POST", Base + "/api/auth/token/refresh", new() { ["X-Refresh-Token"] = _refresh }, null);
            if (st / 100 != 2) return false;
            var data = JsonNode.Parse(bd)?["data"];
            Access = data?["accessToken"]?.GetValue<string>();
            _refresh = data?["refreshToken"]?.GetValue<string>() ?? _refresh;
            return true;
        }

        public JsonNode? Call(string method, string path, object? body = null, Dictionary<string, string>? prms = null, bool retried = false)
        {
            var headers = new Dictionary<string, string>();
            if (ApiKey != null) headers["x-api-key"] = ApiKey;
            else headers["Authorization"] = "Bearer " + Access;
            if (Workspace != null) headers["x-workspace-id"] = Workspace;
            var url = Base + path;
            if (prms != null && prms.Count > 0)
                url += "?" + string.Join("&", prms.Select(kv => Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
            if (Debug)
            {
                var safe = headers.ToDictionary(kv => kv.Key,
                    kv => (kv.Key == "x-api-key" || kv.Key == "Authorization") ? "***" : kv.Value);
                Console.WriteLine($"\n>> {method} {url}");
                Console.WriteLine("   headers: " + ToJson(safe));
                if (body != null) Console.WriteLine("   body   : " + ToJson(body));
            }
            var (st, bd) = Send(method, url, headers, body);
            if (Debug) Console.WriteLine($"<< {st}\n   response: " + (string.IsNullOrEmpty(bd) ? "(empty)" : bd));
            if (st == 401 && !retried && Renew()) return Call(method, path, body, prms, true);
            JsonNode? payload = string.IsNullOrEmpty(bd) ? null : JsonNode.Parse(bd);
            if (st / 100 != 2)
            {
                // Surface the full response body, not just the generic status text.
                var msg = payload?["message"]?.ToString() ?? payload?["error"]?.ToString() ?? Snippet(bd, 500);
                if (!string.IsNullOrEmpty(bd)) msg += "  |  full response: " + bd;
                throw new ApiError(st, msg);
            }
            return payload?["data"];
        }
    }

    static string Snippet(string s, int n) => s == null ? "" : (s.Length > n ? s.Substring(0, n) : s);
    static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // Automation details use plain absolute paths (forward slashes) in
    // sourceItem[].filePath and targetPath — not the {deviceId}_ino_{base64} token.
    static string PlainPath(string rawPath) => (rawPath ?? "").Replace("\\", "/");
    static string NowIso() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'", CultureInfo.InvariantCulture);

    // ============================================================== devices
    // One source -> one target: a single device pair.
    static List<string[]> DevicePairs()
    {
        string source = Require("SOURCE_ID"), sourcePath = Require("SOURCE_PATH");
        string target = Require("TARGET_ID"), targetPath = Require("TARGET_PATH");
        return new List<string[]> { new[] { source, sourcePath, target, targetPath } };
    }

    // ============================================================== file options
    static readonly Dictionary<string, string> DUPLICATE_MAP = new()
    { ["skip"] = "nosend", ["overwrite"] = "overwrite", ["rename"] = "numbering" };

    // SAVE_PATH=device marks the options map here; AutomationDetail() replaces it with the
    // per-pair source device name (the marker never reaches the request body).
    const string SAVEPATH_DEVICE_FLAG = "__savepathDevice";
    static readonly Dictionary<string, string> DEVICE_NAMES = new();

    static (Obj options, bool integrity) FileOptions()
    {
        // Duplicated Name — what to do when the target already has a file with the same name
        var raw = Env("DUPLICATE_ACTION", "overwrite")!.ToLowerInvariant();
        if (!DUPLICATE_MAP.TryGetValue(raw, out var action)) Fail($"DUPLICATE_ACTION must be overwrite|rename|skip, got: {raw}");
        var options = Ob("target-action", DUPLICATE_MAP[raw]);

        // Save Path: `savepath` carries the TARGET SUBFOLDER NAME as a string; `optionPath`
        // only records which mode produced it (1=run date, 2=device name). The source
        // folder structure is preserved under the target path either way.
        var save = Env("SAVE_PATH", "root")!.ToLowerInvariant();
        if (save == "root" || save == "false") { /* no subfolder */ }
        else if (save == "date") { options["savepath"] = RunDateFolder(); options["optionPath"] = 1; }
        else if (save == "device") { options["optionPath"] = 2; options[SAVEPATH_DEVICE_FLAG] = true; }
        else Fail($"SAVE_PATH must be root|date|device, got: {save}");
        return (options, EnvBool("INTEGRITY"));
    }

    static string RunDateFolder() => DateTime.Now.ToString(Env("SAVE_PATH_DATE_FORMAT", "yyMMdd"), CultureInfo.InvariantCulture);

    static void LoadDeviceNames(Api api)
    {
        // deviceId -> device name (GET /api/devices, paginated): savepath must carry the name
        for (int page = 1; page <= 20; page++)
        {
            var result = api.Call("GET", "/api/devices", null,
                new Dictionary<string, string> { ["page"] = page.ToString(), ["size"] = "100" }) as JsonObject;
            foreach (var d in (result?["devices"] as JsonArray) ?? new JsonArray())
            {
                var id = d?["deviceId"]?.ToString();
                if (!string.IsNullOrEmpty(id))
                {
                    var deviceName = d?["name"]?.ToString();
                    DEVICE_NAMES[id!] = string.IsNullOrEmpty(deviceName) ? id! : deviceName!;
                }
            }
            var last = int.TryParse(result?["last_page"]?.ToString(), out var lp) ? lp : 1;
            if (page >= last) return;
        }
    }

    // ============================================================== when it runs
    // Right now: an automation whose schedule is type=none, startDateType=now runs at once.
    static Obj BuildSchedule() =>
        Ob("type", "none", "startDateType", "now", "startDate", NowIso(), "timezone", Env("SCHEDULE_TZ", "Asia/Seoul"));

    // ============================================================== automation body
    static Obj AutomationDetail(string[] pair, Obj options, bool integrity, bool isDir)
    {
        string source = pair[0], sourcePath = pair[1], target = pair[2], targetPath = pair[3];
        var detailOptions = new Obj(options);
        if (detailOptions.Remove(SAVEPATH_DEVICE_FLAG))
        {
            // subfolder per SOURCE device
            detailOptions["savepath"] = DEVICE_NAMES.TryGetValue(source, out var deviceName) ? deviceName : source;
        }
        if (integrity) detailOptions["checkIntegrity"] = true;
        // Plain absolute paths: sourceItem[].filePath for the source, targetPath for the
        // destination (both forward-slashed). targetPath must not be "/" or empty.
        var item = Ob("filePath", PlainPath(sourcePath), "isDir", isDir);
        var to = new Obj { ["noSchedule"] = false };
        foreach (var kv in detailOptions) to[kv.Key] = kv.Value;
        return Ob("senderId", source, "receiverId", target, "sourceItem", Ar(item),
            "targetPath", PlainPath(targetPath), "step", 1, "transferOptions", to);
    }

    static Obj AutomationBody(string name, List<string[]> pairs, Obj options, bool integrity, bool isDir, Obj schedule)
    {
        var details = new Arr();
        foreach (var p in pairs) details.Add(AutomationDetail(p, options, integrity, isDir));
        var body = Ob("name", name, "flowName", name, "transferType", "normal", "timezone", Env("SCHEDULE_TZ", "Asia/Seoul"),
            "details", details, "schedules", Ar(schedule), "step", 1, "isUpcoming", false);
        return body;
    }

    // ============================================================== wait for the run
    static readonly HashSet<int> TERMINAL = new() { 2, 4, 5, 9, 99 };
    static readonly Dictionary<int, string> STATUS = new()
    {
        [-1] = "queued", [0] = "waiting", [1] = "started", [2] = "complete", [3] = "paused", [4] = "error",
        [5] = "cancelled", [6] = "transferring", [7] = "skipped", [8] = "retry", [9] = "partial-complete",
        [10] = "retry-checking", [11] = "virus-scanning", [12] = "syncing", [13] = "incoming", [14] = "activity", [99] = "fail",
    };

    static JsonNode? WaitFor(Api api, string monitorId, int timeoutSec)
    {
        // Poll GET /api/transfers/{monitorId} until terminal; print on change + heartbeat.
        long start = Now(), lastBeat = start, lastChange = start; string? lastState = null;
        while ((Now() - start) / 1000 < timeoutSec)
        {
            var detail = api.Call("GET", $"/api/transfers/{monitorId}");
            int? status = detail?["status"]?.GetValue<int>();
            var percent = detail?["percent"] ?? detail?["progress"];
            string percentStr = percent?.ToString() ?? "0";
            long now = Now();
            var state = $"{status}|{percentStr}";
            bool changed = state != lastState;
            if (changed) { lastState = state; lastChange = now; }
            if (changed || now - lastBeat >= 15000)
            {
                long stalled = (now - lastChange) / 1000;
                var note = stalled >= 60 ? $"  — no progress for {stalled}s; check the target agent/connection" : "";
                Console.WriteLine($"    status: {(status != null && STATUS.ContainsKey(status.Value) ? STATUS[status.Value] : status?.ToString())} ({percentStr}%)  [{(now - start) / 1000}s elapsed]{note}");
                lastBeat = now;
            }
            bool isTerminal = detail?["isTerminal"]?.GetValue<bool>() ?? (status != null && TERMINAL.Contains(status.Value));
            if (isTerminal) return detail;
            Thread.Sleep(3000);
        }
        throw new Exception($"transfer {monitorId} did not finish within {timeoutSec}s (still running — raise WAIT_TIMEOUT, or set WAIT=false to not wait)");
    }

    // Non-transfer summary row kinds in the active list — skip these; a real transfer
    // row has type "monitor" (its top-level monitorId is the id to poll).
    static readonly HashSet<string> SKIP_ROW_TYPES = new() { "automation", "history", "flow" };

    static List<string> ActiveMonitorIds(Api api, string automationId)
    {
        // monitorIds of this automation's active transfers (GET /api/transfers?automationId=...).
        // data = { data: [records], pagination: {...} }; a transfer row is { type: "monitor", monitorId, ... }
        var ids = new List<string>();
        var wrap = api.Call("GET", "/api/transfers", null, new Dictionary<string, string> { ["automationId"] = automationId }) as JsonObject;
        var records = wrap?["data"] as JsonArray;
        if (records != null)
            foreach (var r in records)
            {
                var type = r?["type"]?.ToString();
                if (type != null && SKIP_ROW_TYPES.Contains(type)) continue;
                var m = r?["monitorId"]?.ToString() ?? r?["id"]?.ToString();
                if (!string.IsNullOrEmpty(m)) ids.Add(m!);
            }
        return ids;
    }

    static List<string> ResolveMonitorIds(Api api, string automationId, int expected, int appearWait)
    {
        // Poll until the run(s) start and collect their monitorIds (accumulated across polls,
        // so a short transfer that finishes between polls is not missed).
        long start = Now();
        var seen = new List<string>();
        while (true)
        {
            foreach (var mid in ActiveMonitorIds(api, automationId)) if (!seen.Contains(mid)) seen.Add(mid);
            if (seen.Count >= expected) return seen.GetRange(0, expected);
            if ((Now() - start) / 1000 >= appearWait) return seen;
            Console.WriteLine($"    waiting for the run to start... ({seen.Count}/{expected}) [{(Now() - start) / 1000}s]");
            Thread.Sleep(3000);
        }
    }

    // One transfer per device pair; returns the process exit code.
    static int WaitForRun(Api api, string automationId, int expected)
    {
        if (!EnvBool("WAIT", true)) { Console.WriteLine($"  (WAIT=false) not waiting; find the transfer with GET /api/transfers?automationId={automationId}"); return 0; }
        Console.WriteLine("  waiting for the run to start and complete (polling every 3s; set WAIT=false to skip)...");
        var appearWait = EnvInt("APPEAR_WAIT", 120);
        var monitorIds = ResolveMonitorIds(api, automationId, expected, appearWait);
        if (monitorIds.Count == 0)
        {
            Console.WriteLine($"  ERROR: no transfer started within {appearWait}s — the automation was created but nothing ran. Check GET /api/transfers?automationId={automationId}");
            return 1;
        }
        var missing = expected - monitorIds.Count;
        if (missing > 0)
            Console.WriteLine($"  ERROR: only {monitorIds.Count} of {expected} transfer(s) started within {appearWait}s (raise APPEAR_WAIT if the devices are slow to pick the run up)");
        Console.WriteLine($"  run started: {monitorIds.Count} transfer(s) — {string.Join(", ", monitorIds)}");
        int failed = 0;
        foreach (var monitorId in monitorIds)
        {
            var detail = WaitFor(api, monitorId, EnvInt("WAIT_TIMEOUT", 3600));
            int? status = detail?["status"]?.GetValue<int>();
            var fc = detail?["fileCount"]?.ToString() ?? "0";
            Console.WriteLine($"  finished {monitorId}: {(status != null && STATUS.ContainsKey(status.Value) ? STATUS[status.Value] : status?.ToString())} ({fc} files)");
            if (status != 2) failed++;
        }
        return (failed != 0 || missing > 0) ? 1 : 0;
    }

    // ============================================================== main
    static int Main(string[] args)
    {
        var envPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, ".env");
        if (!File.Exists(envPath)) envPath = ".env";   // fall back to the working directory
        LoadEnvFile(envPath);

        var pairs = DevicePairs();
        bool isDir = EnvBool("SOURCE_IS_DIR", true);
        var name = Env("NAME") ?? "send-one-now";
        var (options, integrity) = FileOptions();

        var api = new Api();
        if (options.ContainsKey(SAVEPATH_DEVICE_FLAG)) LoadDeviceNames(api);
        Console.WriteLine($"[send-one-now]  {pairs.Count} device pair(s)");

        try
        {
            var body = AutomationBody(name, pairs, options, integrity, isDir, BuildSchedule());

            var automationId = api.Call("POST", "/api/automations", body)?["automationId"]?.ToString();
            Console.WriteLine($"automation created: {automationId}");
            return WaitForRun(api, automationId!, pairs.Count);
        }
        catch (ApiError e) { Fail($"API error {e.Status}: {e.Message}"); return 1; }
        catch (Exception e) { Fail(e.Message); return 1; }
    }
}
