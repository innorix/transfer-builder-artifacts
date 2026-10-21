// =============================================================================
//  INNORIX Transfer Builder example — Send Matching Files to One Device When the Previous Transfer Ends
// -----------------------------------------------------------------------------
//  Result page : https://innorix.com/en/builder/send-matching-one-after-transfer
//  Devices     : one source device -> one target device
//  Starts      : when the automation PREV_AUTOMATION_ID finishes
//  Files       : only files that match FILTER_EXT / FILTER_MIN_MB / FILTER_MAX_MB
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
    static double? EnvDouble(string key)
    {
        var v = Env(key);
        if (v == null) return null;
        if (!double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)) Fail($"{key} must be a number, got: {v}");
        return double.Parse(v, CultureInfo.InvariantCulture);
    }
    static List<string> EnvList(string key)
    {
        var v = Env(key);
        return v == null ? new List<string>() : v.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
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

        // Matching files only — Type Filter (extensions, allow/deny) + Size Filter (MB)
        var fo = new Obj();
        var exts = EnvList("FILTER_EXT");
        if (exts.Count > 0)
        {
            bool allow = Env("FILTER_MODE", "allow")!.ToLowerInvariant() != "deny";
            fo["extension"] = Ob("extension", new Arr(exts.Select(e => (object?)e.TrimStart('.').ToLowerInvariant())), "allow", allow);
        }
        var minMb = EnvDouble("FILTER_MIN_MB"); var maxMb = EnvDouble("FILTER_MAX_MB");
        if (minMb != null)
        {
            fo["fileSize"] = Ob("size", (long)(minMb.Value * 1024 * 1024), "over", true, "equal", true);
            if (maxMb != null) Console.WriteLine($"  WARNING: both size bounds set, but the API's fileSize filter takes ONE bound — only FILTER_MIN_MB ({minMb}MB) is applied and FILTER_MAX_MB ({maxMb}MB) is IGNORED, so larger files will be transferred");
        }
        else if (maxMb != null) fo["fileSize"] = Ob("size", (long)(maxMb.Value * 1024 * 1024), "over", false, "equal", true);
        if (fo.Count > 0) options["send-fileoption"] = fo;

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
    // After the previous transfer: chained to PREV_AUTOMATION_ID via triggerAutomation.value.
    static Obj BuildSchedule() =>
        Ob("type", "none", "startDateType", "now", "startDate", NowIso(), "timezone", Env("SCHEDULE_TZ", "Asia/Seoul"),
            "triggerAutomation", Ob("value", Require("PREV_AUTOMATION_ID")));

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
        // workflow id shared by the chain (generated when FLOW_ID is blank)
        body["flowId"] = Env("FLOW_ID") ?? "builder-flow-" + (Now() / 1000);
        return body;
    }

    // ============================================================== main
    static int Main(string[] args)
    {
        var envPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, ".env");
        if (!File.Exists(envPath)) envPath = ".env";   // fall back to the working directory
        LoadEnvFile(envPath);

        var pairs = DevicePairs();
        bool isDir = EnvBool("SOURCE_IS_DIR", true);
        var name = Env("NAME") ?? "send-matching-one-after-transfer";
        var (options, integrity) = FileOptions();

        var api = new Api();
        if (options.ContainsKey(SAVEPATH_DEVICE_FLAG)) LoadDeviceNames(api);
        Console.WriteLine($"[send-matching-one-after-transfer]  {pairs.Count} device pair(s)");

        try
        {
            var body = AutomationBody(name, pairs, options, integrity, isDir, BuildSchedule());

            var automationId = api.Call("POST", "/api/automations", body)?["automationId"]?.ToString();
            Console.WriteLine($"automation created: {automationId}");
            Console.WriteLine($"  chained after automation: {Env("PREV_AUTOMATION_ID")} (flowId {body["flowId"]})");
            return 0;
        }
        catch (ApiError e) { Fail($"API error {e.Status}: {e.Message}"); return 1; }
        catch (Exception e) { Fail(e.Message); return 1; }
    }
}
