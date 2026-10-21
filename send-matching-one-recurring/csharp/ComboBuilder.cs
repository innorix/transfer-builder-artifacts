// =============================================================================
//  INNORIX Transfer Builder example (ComboBuilder.cs)  —  C# port
// -----------------------------------------------------------------------------
//  Runs the configuration you picked in the INNORIX "Transfer Builder" from a
//  single .env file. Behaves the same as the Python version (combo_builder.py).
//  See the README (README.md) for details.
//
//  Requirements:  .NET 8+ (standard library only, no external dependencies)
//  Run:
//    dotnet run                        # use the .env in this folder
//    dotnet run -- path/to/x.env       # or point it at another .env
//  (Keep it in the same folder as combo_builder.csproj.)
//
//  Two axes: TRANSFER_TYPE(send_one/send_many/collect/sync) x START_WHEN(now/schedule/
//  repeat/after_previous/external). Authenticate with INNORIX_API_KEY (x-api-key), or
//  INNORIX_ACCESS_TOKEN (Bearer), or INNORIX_EMAIL + INNORIX_PASSWORD login.
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
    static Obj EnvJson(string key)
    {
        var v = Env(key);
        if (v == null) return new Obj();
        try
        {
            var node = JsonNode.Parse(v) as JsonObject;
            if (node == null) { Fail($"{key} must be a JSON object"); return new Obj(); }
            return NodeToObj(node);
        }
        catch (Exception e) { Fail($"{key} must be valid JSON: {e.Message}"); return new Obj(); }
    }
    static string Require(string key)
    {
        var v = Env(key);
        if (v == null) Fail($"missing required setting in .env: {key}");
        return v!;
    }
    static void Fail(string msg) { Console.Error.WriteLine(msg); Environment.Exit(1); }

    // convert a parsed JsonObject into our Obj/Arr tree (for *_CONFIG round-tripping)
    static Obj NodeToObj(JsonObject o)
    {
        var m = new Obj();
        foreach (var kv in o) m[kv.Key] = NodeToVal(kv.Value);
        return m;
    }
    static object? NodeToVal(JsonNode? n)
    {
        switch (n)
        {
            case null: return null;
            case JsonObject o: return NodeToObj(o);
            case JsonArray a: return a.Select(NodeToVal).ToList();
            case JsonValue v:
                if (v.TryGetValue<bool>(out var b)) return b;
                if (v.TryGetValue<long>(out var l)) return l;
                if (v.TryGetValue<double>(out var d)) return d;
                return v.ToString();
            default: return n.ToString();
        }
    }

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

    static readonly HashSet<int> TERMINAL = new() { 2, 4, 5, 9, 99 };
    static readonly Dictionary<int, string> STATUS = new()
    {
        [-1] = "queued", [0] = "waiting", [1] = "started", [2] = "complete", [3] = "paused", [4] = "error",
        [5] = "cancelled", [6] = "transferring", [7] = "skipped", [8] = "retry", [9] = "partial-complete",
        [10] = "retry-checking", [11] = "virus-scanning", [12] = "syncing", [13] = "incoming", [14] = "activity", [99] = "fail",
    };

    static JsonNode? WaitFor(Api api, string monitorId, int timeoutSec)
    {
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
    static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // Automation details use plain absolute paths (forward slashes) in
    // sourceItem[].filePath and targetPath — not the {deviceId}_ino_{base64} token.
    static string PlainPath(string rawPath) => (rawPath ?? "").Replace("\\", "/");
    static string NowIso() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'", CultureInfo.InvariantCulture);

    // ============================================================== devices (axis 1)
    // Line a *_PATHS list up with its *_IDS list: 1 path = same for all, N = one each.
    static List<string> ExpandPaths(List<string> paths, int count, string pathsKey, string idsKey)
    {
        if (paths.Count == 0) Fail($"{pathsKey} is required for this transfer type (one path, or one per {idsKey})");
        if (paths.Count == 1) return Enumerable.Repeat(paths[0], count).ToList();
        if (paths.Count == count) return paths;
        Fail($"{pathsKey} must have 1 entry (same for all) or exactly {count} (one per {idsKey})");
        return paths;   // unreachable (Fail exits)
    }

    // Resolve the device list + its paths, with a fallback to the single keys: if the *_IDS
    // list is empty use the single *_ID, and if the *_PATHS list is empty use the single *_PATH.
    static (List<string> ids, List<string> paths) ResolveList(string listIdsKey, string singleIdKey, string listPathsKey, string singlePathKey, string kind)
    {
        var ids = EnvList(listIdsKey);
        if (ids.Count == 0)
        {
            var one = Env(singleIdKey);
            if (one == null) Fail($"{kind} needs {listIdsKey} (or a single {singleIdKey})");
            ids = new List<string> { one! };
        }
        var paths = EnvList(listPathsKey);
        if (paths.Count == 0 && Env(singlePathKey) != null) paths = new List<string> { Env(singlePathKey)! };
        paths = ExpandPaths(paths, ids.Count, $"{listPathsKey} (or {singlePathKey})", listIdsKey);
        return (ids, paths);
    }

    static List<string[]> DevicePairs(string transferType)
    {
        // send_many : one source (SOURCE_ID + SOURCE_PATH) + target list (TARGET_IDS + TARGET_PATHS)
        // collect   : one target (TARGET_ID + TARGET_PATH) + source list (SOURCE_IDS + SOURCE_PATHS)
        // send_one/sync : single SOURCE_* and TARGET_* pair
        var pairs = new List<string[]>();
        if (transferType == "send_many")
        {
            string source = Require("SOURCE_ID"), sourcePath = Require("SOURCE_PATH");
            var (targets, paths) = ResolveList("TARGET_IDS", "TARGET_ID", "TARGET_PATHS", "TARGET_PATH", "send_many");
            for (int i = 0; i < targets.Count; i++) pairs.Add(new[] { source, sourcePath, targets[i], paths[i] });
        }
        else if (transferType == "collect")
        {
            string target = Require("TARGET_ID"), targetPath = Require("TARGET_PATH");
            var (sources, paths) = ResolveList("SOURCE_IDS", "SOURCE_ID", "SOURCE_PATHS", "SOURCE_PATH", "collect");
            for (int i = 0; i < sources.Count; i++) pairs.Add(new[] { sources[i], paths[i], target, targetPath });
        }
        else   // send_one, sync
        {
            string source = Require("SOURCE_ID"), sourcePath = Require("SOURCE_PATH");
            string target = Require("TARGET_ID"), targetPath = Require("TARGET_PATH");
            pairs.Add(new[] { source, sourcePath, target, targetPath });
        }
        return pairs;
    }

    // ============================================================== file options
    static readonly Dictionary<string, string> DUPLICATE_MAP = new()
    { ["skip"] = "nosend", ["overwrite"] = "overwrite", ["rename"] = "numbering", ["nosend"] = "nosend", ["numbering"] = "numbering" };

    static (Obj options, bool integrity) FileOptions()
    {
        var raw = Env("DUPLICATE_ACTION", "overwrite")!.ToLowerInvariant();
        if (!DUPLICATE_MAP.TryGetValue(raw, out var action)) { Fail($"DUPLICATE_ACTION must be overwrite|rename|skip, got: {raw}"); }
        var options = Ob("target-action", DUPLICATE_MAP[raw]);

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
        var exclude = Env("FILTER_EXCLUDE");
        if (exclude != null) fo["fileName"] = Ob("name", exclude, "allow", false);
        if (fo.Count > 0) options["send-fileoption"] = fo;

        // Save Path: `savepath` carries the TARGET SUBFOLDER NAME as a string; `optionPath`
        // only records which mode produced it (1=run date, 2=device name, 3=custom). The server
        // does not build the name from optionPath, so a boolean would create a folder literally
        // named "true". The source folder structure is preserved under the target path either way.
        var save = Env("SAVE_PATH", "false")!.ToLowerInvariant();
        if (new[] { "false", "off", "no", "0", "true", "on", "yes", "1", "root" }.Contains(save)) { /* no subfolder */ }
        else if (new[] { "date", "yymmdd", "start_date" }.Contains(save)) { options["savepath"] = RunDateFolder(); options["optionPath"] = 1; }
        else if (new[] { "device", "device_name" }.Contains(save)) { options["optionPath"] = 2; options[SAVEPATH_DEVICE_FLAG] = true; }
        else if (new[] { "custom", "custom_name" }.Contains(save)) { options["savepath"] = Require("SAVE_PATH_NAME"); options["optionPath"] = 3; }
        else if (new[] { "transfer-id", "transfer_id", "transferid" }.Contains(save))
            Fail("SAVE_PATH=transfer-id is not supported: the target subfolder name must be known before the transfer is created, so the transfer ID cannot be used. Use date, device or custom.");
        else Fail($"SAVE_PATH must be root|date|device|custom (or true/false), got: {save}");
        return (options, EnvBool("INTEGRITY"));
    }

    // ============================================================== after transfer
    static readonly string[][] INTEGRATIONS =
    {
        new[] { "ON_MESSAGE", "notification", "MESSAGE" },
        new[] { "ON_VIRUS_SCAN", "security", "VIRUS_SCAN" },
        new[] { "ON_EMAIL", "email", "EMAIL" },
    };

    // <PREFIX>_EVENTS (per action, from the builder's Timing) wins over the shared NOTIFY_EVENTS
    static Obj NotifyEvents(string? prefix = null)
    {
        var perAction = prefix == null ? new List<string>() : EnvList($"{prefix}_EVENTS");
        var evs = perAction.Count > 0 ? perAction
            : EnvList("NOTIFY_EVENTS").Count > 0 ? EnvList("NOTIFY_EVENTS") : new List<string> { "completed", "error" };
        var m = new Obj();
        foreach (var e in evs) m[e] = true;
        return m;
    }

    static void EnsureIntegrations(Api api)
    {
        foreach (var row in INTEGRATIONS)
        {
            string toggle = row[0], category = row[1], prefix = row[2];
            if (!EnvBool(toggle)) continue;
            var events = NotifyEvents(prefix);
            var provider = Require(prefix + "_TYPE").ToLowerInvariant();
            var config = EnvJson(prefix + "_CONFIG");
            var name = Env(prefix + "_NAME") ?? (config.TryGetValue("name", out var cn) ? cn?.ToString() : null) ?? $"builder-{provider}";
            if (!config.ContainsKey("name")) config["name"] = name;

            var rules = api.Call("GET", $"/api/integrations/rules/{provider}") as JsonObject;
            JsonNode? rule = rules?[provider] ?? rules?.FirstOrDefault().Value;
            if (rule == null) Console.WriteLine($"  ! {toggle}: unknown provider '{provider}' — check {prefix}_TYPE");
            else
            {
                var missing = new List<string>();
                foreach (var f in (rule["fields"] as JsonArray) ?? new JsonArray())
                    if (f?["required"]?.GetValue<bool>() == true && !config.ContainsKey(f?["id"]?.ToString() ?? "")) missing.Add(f?["id"]?.ToString() ?? "");
                if (missing.Count > 0)
                {
                    var detail = $"{toggle}: {prefix}_CONFIG is missing required fields: {string.Join(", ", missing)} — the integration is saved but will not work";
                    if (EnvBool("INTEGRATION_STRICT")) Fail($"  ! {detail} (INTEGRATION_STRICT=true)");
                    Console.WriteLine($"  ! {detail}");
                }
            }

            bool reused = false;
            foreach (var it in (api.Call("GET", "/api/integrations", null, new() { ["category"] = category }) as JsonArray) ?? new JsonArray())
                if (it?["type"]?.ToString() == provider && it?["name"]?.ToString() == name)
                { Console.WriteLine($"  {toggle}: {category}/{provider} '{name}' already exists, reused"); reused = true; break; }
            if (reused) continue;

            var reqBody = Ob("name", name, "type", provider, "category", category, "config", config, "notificationConfig", Ob("events", events));
            foreach (var k in new[] { "webhookUrl", "baseUrl", "mode", "authType" }) if (config.ContainsKey(k)) reqBody[k] = config[k];
            if (config.ContainsKey("webhookUrl") && !reqBody.ContainsKey("mode")) reqBody["mode"] = "webhook";
            var result = api.Call("POST", "/api/integrations", reqBody);
            var id = result?["integrationId"]?.ToString() ?? result?["id"]?.ToString();
            Console.WriteLine($"  {toggle}: {category}/{provider} '{name}' created ({id})");
        }
    }

    // SAVE_PATH=device marks the options map here; AutomationDetail() replaces it with the
    // per-pair source device name (the marker never reaches the request body).
    const string SAVEPATH_DEVICE_FLAG = "__savepathDevice";
    static readonly Dictionary<string, string> DEVICE_NAMES = new();

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

    static Obj? RunApiProcessor()
    {
        // Public API spec: a run action needs category="run" and type="http"|"command"|"script",
        // and every parameter goes under `config` (url / method / body / deviceId / events /
        // waitForResult). Top-level url/method are deprecated, and a processor without
        // category="run" is dropped by the server without an error.
        if (!EnvBool("ON_RUN_API")) return null;
        var action = Env("RUN_API_TYPE", "http")!.ToLowerInvariant();
        if (action == "https") action = "http";                 // builder used to emit this
        if (!new[] { "http", "command", "script" }.Contains(action))
            Fail($"RUN_API_TYPE must be http|command|script, got: {action}");

        var config = new Obj();
        if (action == "http")
        {
            config["url"] = Require("RUN_API_URL");
            config["method"] = Env("RUN_API_METHOD", "POST")!.ToUpperInvariant();
            var body = Env("RUN_API_BODY");
            if (!string.IsNullOrEmpty(body)) config["body"] = EnvJson("RUN_API_BODY");
        }
        else if (action == "command") config["command"] = Require("RUN_API_COMMAND");
        else
        {
            config["file"] = Require("RUN_API_SCRIPT");
            var args = Env("RUN_API_ARGS");
            if (!string.IsNullOrEmpty(args)) config["args"] = args;
        }

        // Run On: source device = senderId, target device = receiverId (omit -> the transfer's device)
        var device = Env("RUN_API_DEVICE", "")!.ToLowerInvariant();
        if (new[] { "source", "sender" }.Contains(device)) config["deviceId"] = Require("SOURCE_ID");
        else if (new[] { "target", "receiver" }.Contains(device)) config["deviceId"] = Require("TARGET_ID");
        else if (device.Length > 0) config["deviceId"] = Env("RUN_API_DEVICE");

        // config.events = {"completed": true, ...}; omit to fire on every event.
        // "Run" is a builder label, not a transfer event, so it is ignored here.
        var events = new Obj();
        foreach (var e in EnvList("RUN_API_EVENTS"))
            if (!string.Equals(e, "run", StringComparison.OrdinalIgnoreCase)) events[e] = true;
        if (events.Count > 0) config["events"] = events;
        if (EnvBool("RUN_API_WAIT")) config["waitForResult"] = true;

        return Ob("category", "run", "type", action, "name", Env("RUN_API_NAME", "builder-run-api"), "config", config);
    }

    static Obj? MonitoringProcessor()
    {
        if (!EnvBool("ON_MONITORING")) return null;
        var provider = Require("MONITORING_TYPE").ToLowerInvariant();
        var config = EnvJson("MONITORING_CONFIG");
        var name = Env("MONITORING_NAME") ?? (config.TryGetValue("name", out var cn) ? cn?.ToString() : null) ?? $"builder-{provider}";
        if (!config.ContainsKey("name")) config["name"] = name;
        return Ob("category", "monitoring", "type", provider, "name", name, "config", config, "notificationConfig", Ob("events", NotifyEvents("MONITORING")));
    }

    static Arr AfterTransferProcessors()
    {
        var ps = new Arr();
        var r = RunApiProcessor(); if (r != null) ps.Add(r);
        var m = MonitoringProcessor(); if (m != null) ps.Add(m);
        return ps;
    }

    // ============================================================== schedule (axis 2)
    static readonly Dictionary<string, string> WEEKDAY = new()
    { ["sun"] = "sunday", ["mon"] = "monday", ["tue"] = "tuesday", ["wed"] = "wednesday", ["thu"] = "thursday", ["fri"] = "friday", ["sat"] = "saturday" };
    // C# DayOfWeek: Sunday=0 .. Saturday=6
    static readonly Dictionary<string, int> WEEKDAY_INDEX = new()
    { ["sunday"] = 0, ["monday"] = 1, ["tuesday"] = 2, ["wednesday"] = 3, ["thursday"] = 4, ["friday"] = 5, ["saturday"] = 6 };

    static Obj Clock() => Ob("hour", Env("REPEAT_HOUR", "12"), "minute", Env("REPEAT_MINUTE", "00"),
        "ampm", Env("REPEAT_AMPM", "am")!.ToLowerInvariant(), "timezone", Env("SCHEDULE_TZ", "Asia/Seoul"));
    static int Hour24() => int.Parse(Env("REPEAT_HOUR", "12")!) % 12 + (Env("REPEAT_AMPM", "am")!.ToLowerInvariant() == "pm" ? 12 : 0);

    static DateTime NowInTz(string tz)
    {
        try { return TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(tz)); }
        catch { Console.WriteLine($"  note: timezone '{tz}' unavailable; computing first run in local time"); return DateTime.Now; }
    }
    static DateTime NextOccurrence(string every, HashSet<int> weekdays, HashSet<int> doms)
    {
        var now = NowInTz(Env("SCHEDULE_TZ", "Asia/Seoul")!);
        if (every == "hour")
        {
            var top = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            return top > now ? top : top.AddHours(1);
        }
        var baseDt = new DateTime(now.Year, now.Month, now.Day, Hour24(), int.Parse(Env("REPEAT_MINUTE", "00")!), 0);
        for (int i = 0; i < 400; i++)
        {
            var cand = baseDt.AddDays(i);
            if (cand <= now) continue;
            if (every == "day") return cand;
            if (every == "week" && weekdays.Contains((int)cand.DayOfWeek)) return cand;
            if (every == "month")
            {
                int last = DateTime.DaysInMonth(cand.Year, cand.Month);
                if (doms.Contains(cand.Day) || (doms.Contains(0) && cand.Day == last)) return cand;
            }
        }
        return baseDt;
    }
    static Obj StartFields(string every, HashSet<int> weekdays, HashSet<int> doms)
    {
        var startAt = Env("REPEAT_START_AT");
        if (startAt != null) return Ob("startDateType", "specific", "startDate", startAt);
        if (EnvBool("REPEAT_RUN_NOW", false)) return Ob("startDateType", "now", "startDate", NowIso());
        var first = NextOccurrence(every, weekdays, doms).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        Console.WriteLine($"  first run at {first} ({Env("SCHEDULE_TZ", "Asia/Seoul")}); no immediate run");
        return Ob("startDateType", "specific", "startDate", first);
    }
    static Obj BuildRepeatSchedule()
    {
        var every = Env("REPEAT_EVERY", "day")!.ToLowerInvariant();
        if (!new[] { "hour", "day", "week", "month" }.Contains(every)) Fail($"REPEAT_EVERY must be hour|day|week|month, got: {every}");
        var weekdays = new HashSet<int>(); var doms = new HashSet<int>();
        var sched = new Obj { ["type"] = every };
        foreach (var kv in Clock()) sched[kv.Key] = kv.Value;
        if (every == "week")
        {
            var names = new Arr();
            var ws = EnvList("REPEAT_WEEKDAY").Count > 0 ? EnvList("REPEAT_WEEKDAY") : new List<string> { "monday" };
            foreach (var d in ws)
            {
                var key = d.ToLowerInvariant().Substring(0, Math.Min(3, d.Length));
                if (!WEEKDAY.ContainsKey(key)) Fail($"REPEAT_WEEKDAY: invalid weekday '{d}' (sunday..saturday)");
                names.Add(WEEKDAY[key]); weekdays.Add(WEEKDAY_INDEX[WEEKDAY[key]]);
            }
            sched["dayInWeek"] = names;
        }
        else if (every == "month")
        {
            var dates = new Arr();
            var ds = EnvList("REPEAT_DAY_OF_MONTH").Count > 0 ? EnvList("REPEAT_DAY_OF_MONTH") : new List<string> { "1" };
            foreach (var d in ds)
            {
                var day = new[] { "lastday", "last", "last_day", "eom" }.Contains(d.Trim().ToLowerInvariant()) ? "0" : d;   // builder's "Last day"
                if (!day.All(char.IsDigit) || int.Parse(day) < 0 || int.Parse(day) > 31) Fail($"REPEAT_DAY_OF_MONTH: invalid day '{d}' (1-31, 0 or 'lastday' = last day)");
                dates.Add(int.Parse(day).ToString()); doms.Add(int.Parse(day));
            }
            sched["dayInMonth"] = dates;
        }
        foreach (var kv in StartFields(every, weekdays, doms)) sched[kv.Key] = kv.Value;
        return sched;
    }
    static Obj? BuildSchedule(string startWhen)
    {
        var tz = Env("SCHEDULE_TZ", "Asia/Seoul");
        if (startWhen == "schedule")
        {
            // the clock fields must travel with startDate: without them the server falls back to
            // its own default clock (12 pm UTC) and runs at the wrong hour — and on the wrong day
            var at = Require("SCHEDULE_AT");
            var sc = Ob("type", "none", "startDateType", "specific", "startDate", at, "timezone", tz);
            foreach (var kv in ClockFrom(at)) sc[kv.Key] = kv.Value;
            return sc;
        }
        if (startWhen == "repeat") return BuildRepeatSchedule();
        if (startWhen == "after_previous") return Ob("type", "none", "startDateType", "now", "startDate", NowIso(), "timezone", tz, "triggerAutomation", Ob("value", Require("PREV_AUTOMATION_ID")));
        // startDateType must NOT be "now" for a command trigger — that runs the automation once at creation
        if (startWhen == "external") return Ob("type", "none", "startDateType", "specific", "startDate", NowIso(), "timezone", tz);
        return null;
    }

    // ============================================================== automation body
    static Obj AutomationDetail(string[] pair, Obj options, bool integrity, bool isDir, bool isSync)
    {
        string source = pair[0], sourcePath = pair[1], target = pair[2], targetPath = pair[3];
        var detailOptions = new Obj(options);
        if (detailOptions.Remove(SAVEPATH_DEVICE_FLAG))
        {
            // subfolder per SOURCE device, so a collect (N:1) keeps each sender separate
            detailOptions["savepath"] = DEVICE_NAMES.TryGetValue(source, out var deviceName) ? deviceName : source;
        }
        if (integrity) detailOptions["checkIntegrity"] = true;
        if (isSync)
        {
            detailOptions["syncType"] = Env("SYNC_DIRECTION", "one_way")!.ToLowerInvariant() == "two_way" ? 2 : 1;
            var watch = Env("SYNC_WATCH", "file_created")!.ToLowerInvariant();
            var watchMap = new Dictionary<string, int> { ["file_created"] = 1, ["file_modified"] = 2, ["both"] = 3 };  // 3=both needs server support
            if (!watchMap.ContainsKey(watch)) Fail($"SYNC_WATCH must be file_created|file_modified|both, got: {watch}");
            detailOptions["watchFolderType"] = watchMap[watch];
        }
        // Plain absolute paths: sourceItem[].filePath for the source, targetPath for the
        // destination (both forward-slashed). targetPath must not be "/" or empty.
        var item = Ob("filePath", PlainPath(sourcePath), "isDir", isDir);
        var to = new Obj { ["noSchedule"] = false };
        foreach (var kv in detailOptions) to[kv.Key] = kv.Value;
        return Ob("senderId", source, "receiverId", target, "sourceItem", Ar(item),
            "targetPath", PlainPath(targetPath), "step", 1, "transferOptions", to);
    }

    static Obj AutomationBody(string name, List<string[]> pairs, Obj options, bool integrity, bool isDir, Obj? schedule, Arr processors, bool isSync)
    {
        var startWhen = Env("START_WHEN");
        var transferType = isSync ? "sync" : (startWhen == "external" ? "command" : "normal");
        var tz = Env("SCHEDULE_TZ", "Asia/Seoul");
        var details = new Arr();
        foreach (var p in pairs) details.Add(AutomationDetail(p, options, integrity, isDir, isSync));
        var sched = schedule ?? Ob("type", "none", "startDateType", "now", "startDate", NowIso(), "timezone", tz);
        var body = Ob("name", name, "flowName", name, "transferType", transferType, "timezone", tz,
            "details", details, "schedules", Ar(sched), "step", 1, "isUpcoming", false);
        var flowId = Env("FLOW_ID");
        if (startWhen == "after_previous" && flowId == null) flowId = "builder-flow-" + (Now() / 1000);
        if (flowId != null) body["flowId"] = flowId;
        if (processors.Count > 0) body["processors"] = processors;
        return body;
    }

    // ============================================================== immediate transfer
    static int WaitAll(Api api, List<string> monitorIds)
    {
        int failed = 0;
        foreach (var monitorId in monitorIds)
        {
            var detail = WaitFor(api, monitorId, EnvInt("WAIT_TIMEOUT", 3600));
            int? status = detail?["status"]?.GetValue<int>();
            var fc = detail?["fileCount"]?.ToString() ?? "0";
            Console.WriteLine($"  finished {monitorId}: {(status != null && STATUS.ContainsKey(status.Value) ? STATUS[status.Value] : status?.ToString())} ({fc} files)");
            if (status != 2) failed++;
        }
        return failed > 0 ? 1 : 0;
    }

    // Non-transfer summary row kinds in the active list — skip these; a real transfer
    // row has type "monitor" (its top-level monitorId is the id to poll).
    static readonly HashSet<string> SKIP_ROW_TYPES = new() { "automation", "history", "flow" };

    static List<string> ActiveMonitorIds(Api api, string automationId)
    {
        // monitorIds of this automation's currently-active transfers, from the active-transfer
        // list GET /api/transfers?automationId=... . This shows a transfer as soon as it STARTS
        // (unlike the execution log, which reflects finished runs). The response is
        // cursor-paginated: data = { data: [records], pagination: {...} } and a transfer row
        // looks like { type: "monitor", monitorId: "T...", status: 1, ... }.
        var ids = new List<string>();
        var wrap = api.Call("GET", "/api/transfers", null, new Dictionary<string, string> { ["automationId"] = automationId }) as JsonObject;
        var records = wrap?["data"] as JsonArray;
        if (records != null)
            foreach (var r in records)
            {
                var type = r?["type"]?.ToString();
                if (type != null && SKIP_ROW_TYPES.Contains(type)) continue;       // skip summary rows
                var m = r?["monitorId"]?.ToString() ?? r?["id"]?.ToString();       // monitorId (== id) is what we poll
                if (!string.IsNullOrEmpty(m)) ids.Add(m!);
            }
        return ids;
    }

    static List<string> ResolveMonitorIds(Api api, string automationId, int expected, int appearWait)
    {
        // Poll the active-transfer list until the run(s) start and collect their monitorIds
        // (accumulated across polls, so a short transfer that finishes between polls is not missed).
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

    static int RunImmediateAutomation(Api api, string name, List<string[]> pairs, Obj options, bool integrity, bool isDir, Arr processors)
    {
        // START_WHEN=now -> ONE POST /api/automations with a type=none,
        // startDateType=now schedule (runs immediately). All pairs go in details[].
        var tz = Env("SCHEDULE_TZ", "Asia/Seoul");
        var schedule = Ob("type", "none", "startDateType", "now", "startDate", NowIso(), "timezone", tz);
        var body = AutomationBody(name, pairs, options, integrity, isDir, schedule, processors, false);
        var automationId = api.Call("POST", "/api/automations", body)?["automationId"]?.ToString();
        Console.WriteLine($"  automation created: {automationId}  (immediate: schedule type=none, startDateType=now)");

        if (!EnvBool("WAIT", true)) { Console.WriteLine($"  (WAIT=false) not waiting; find the transfer with GET /api/transfers?automationId={automationId}"); return 0; }
        Console.WriteLine("  waiting for the run to start and complete (polling every 3s; set WAIT=false to skip)...");
        var appearWait = EnvInt("APPEAR_WAIT", 120);
        var monitorIds = ResolveMonitorIds(api, automationId!, pairs.Count, appearWait);
        if (monitorIds.Count == 0)
        {
            Console.WriteLine($"  ERROR: no transfer started within {appearWait}s — the automation was created but nothing ran. Check GET /api/transfers?automationId={automationId}");
            return 1;
        }
        var missing = pairs.Count - monitorIds.Count;
        if (missing > 0)
            Console.WriteLine($"  ERROR: only {monitorIds.Count} of {pairs.Count} transfer(s) started within {appearWait}s (raise APPEAR_WAIT if the devices are slow to pick the run up)");
        Console.WriteLine($"  run started: {monitorIds.Count} transfer(s) — {string.Join(", ", monitorIds)}");
        var failed = WaitAll(api, monitorIds);
        return (failed != 0 || missing > 0) ? 1 : 0;
    }

    // ============================================================== main
    static readonly string[] TRANSFER_TYPES = { "send_one", "send_many", "collect", "sync" };
    static readonly string[] START_WHENS = { "now", "schedule", "repeat", "after_previous", "external" };

    static int Main(string[] args)
    {
        var envPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, ".env");
        if (!File.Exists(envPath)) envPath = ".env";   // fall back to the working directory
        LoadEnvFile(envPath);

        var transferType = Env("TRANSFER_TYPE", "send_one")!.ToLowerInvariant();
        if (!TRANSFER_TYPES.Contains(transferType)) Fail($"TRANSFER_TYPE must be {string.Join("|", TRANSFER_TYPES)}, got: {transferType}");
        bool isSync = transferType == "sync";

        var startWhen = Env("START_WHEN", "now")!.ToLowerInvariant();
        if (!isSync && !START_WHENS.Contains(startWhen)) Fail($"START_WHEN must be {string.Join("|", START_WHENS)}, got: {startWhen}");

        var pairs = DevicePairs(transferType);
        bool isDir = EnvBool("SOURCE_IS_DIR", true);
        var name = Env("NAME") ?? $"builder {transferType} {startWhen}";
        var (options, integrity) = FileOptions();
        var processors = AfterTransferProcessors();

        var api = new Api();
        if (options.ContainsKey(SAVEPATH_DEVICE_FLAG)) LoadDeviceNames(api);
        Console.WriteLine($"[{transferType} / {(isSync ? "sync" : startWhen)}]  {pairs.Count} device pair(s)");

        try
        {
            if (INTEGRATIONS.Any(r => EnvBool(r[0]))) { Console.WriteLine("integrations:"); EnsureIntegrations(api); }

            if (!isSync && startWhen == "now")
            {
                Console.WriteLine("transfer:");
                return RunImmediateAutomation(api, name, pairs, options, integrity, isDir, processors);
            }

            Obj? schedule = isSync ? null : BuildSchedule(startWhen);
            var body = AutomationBody(name, pairs, options, integrity, isDir, schedule, processors, isSync);

            if (!isSync && startWhen == "external")
            {
                var code = api.Call("POST", "/api/command/generate-code")?["code"]?.ToString();
                var apiKey = api.Call("GET", "/api/command/generate-api-key")?["apiKey"]?.ToString();
                if (code == null || apiKey == null) Fail("failed to issue command code / api key");
                body["code"] = code; body["apiKey"] = apiKey;
            }

            var automationId = api.Call("POST", "/api/automations", body)?["automationId"]?.ToString();
            Console.WriteLine($"automation created: {automationId}");

            if (isSync)
            {
                var direction = Env("SYNC_DIRECTION", "one_way")!.ToLowerInvariant() == "two_way" ? "two-way" : "one-way";
                var w = Env("SYNC_WATCH", "file_created")!.ToLowerInvariant();
                var watch = w == "file_modified" ? "on file modify" : w == "both" ? "on create and modify" : "on file create";
                Console.WriteLine($"  sync: {direction}, {watch}");
            }
            else if (startWhen == "schedule") Console.WriteLine($"  schedule: once at {Env("SCHEDULE_AT")}");
            else if (startWhen == "repeat")
            {
                var s = new Obj(schedule!); s.Remove("startDate"); s.Remove("startDateType");
                Console.WriteLine($"  repeat: every {Env("REPEAT_EVERY", "day")} {ToJson(s)}");
            }
            else if (startWhen == "after_previous") Console.WriteLine($"  chained after automation: {Env("PREV_AUTOMATION_ID")} (flowId {body["flowId"]})");
            else if (startWhen == "external")
            {
                var triggerBase = Env("INNORIX_TRIGGER_BASE_URL", api.Base)!.TrimEnd('/');
                var url = $"{triggerBase}/command/{body["code"]}";
                Console.WriteLine($"  endpoint URL : {url}");
                Console.WriteLine($"  x-api-key    : {body["apiKey"]}   (secret — anyone with it can start this transfer)");
                if (EnvBool("FIRE_TRIGGER"))
                {
                    var (st, bd) = api.Send("POST", url, new() { ["x-api-key"] = body["apiKey"]!.ToString()! }, null);
                    Console.WriteLine($"  trigger fired: {st} {Snippet(bd, 120)}");
                }
            }
            return 0;
        }
        catch (ApiError e) { Fail($"API error {e.Status}: {e.Message}"); return 1; }
        catch (Exception e) { Fail(e.Message); return 1; }
    }
}
