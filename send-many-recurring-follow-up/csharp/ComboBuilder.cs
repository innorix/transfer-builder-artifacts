// =============================================================================
//  INNORIX Transfer Builder example — Send Files to Many Devices on a Recurring Schedule With Follow-Up Actions
// -----------------------------------------------------------------------------
//  Result page : https://innorix.com/en/builder/send-many-recurring-follow-up
//  Devices     : one source device -> several target devices (TARGET_IDS)
//  Starts      : on a recurring schedule (REPEAT_EVERY hour | day | week | month)
//  After       : follow-up actions (Run API, Monitoring, Message, Virus Scan, Email)
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

    // Automation details use plain absolute paths (forward slashes) in
    // sourceItem[].filePath and targetPath — not the {deviceId}_ino_{base64} token.
    static string PlainPath(string rawPath) => (rawPath ?? "").Replace("\\", "/");
    static string NowIso() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.000'Z'", CultureInfo.InvariantCulture);

    // ============================================================== devices
    // Line a *_PATHS list up with its *_IDS list: 1 path = same for all, N = one each.
    static List<string> ExpandPaths(List<string> paths, int count, string pathsKey, string idsKey)
    {
        if (paths.Count == 0) Fail($"{pathsKey} is required (one path, or one per {idsKey})");
        if (paths.Count == 1) return Enumerable.Repeat(paths[0], count).ToList();
        if (paths.Count == count) return paths;
        Fail($"{pathsKey} must have 1 entry (same for all) or exactly {count} (one per {idsKey})");
        return paths;   // unreachable (Fail exits)
    }

    // One source (SOURCE_ID + SOURCE_PATH) -> every device in TARGET_IDS (+ TARGET_PATHS).
    static List<string[]> DevicePairs()
    {
        string source = Require("SOURCE_ID"), sourcePath = Require("SOURCE_PATH");
        var targets = EnvList("TARGET_IDS");
        if (targets.Count == 0) Fail("missing required setting in .env: TARGET_IDS");
        var paths = ExpandPaths(EnvList("TARGET_PATHS"), targets.Count, "TARGET_PATHS", "TARGET_IDS");
        var pairs = new List<string[]>();
        for (int i = 0; i < targets.Count; i++) pairs.Add(new[] { source, sourcePath, targets[i], paths[i] });
        return pairs;
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

    // ============================================================== after transfer (follow-up actions)
    // Message / Virus Scan / Email are workspace-wide integrations (POST /api/integrations)
    // that fire for the events in NOTIFY_EVENTS. Run API and Monitoring are processors
    // attached to this automation (see AfterTransferProcessors()).
    static readonly string[][] INTEGRATIONS =
    {
        new[] { "ON_MESSAGE", "notification", "MESSAGE" },
        new[] { "ON_VIRUS_SCAN", "security", "VIRUS_SCAN" },
        new[] { "ON_EMAIL", "email", "EMAIL" },
    };

    // <PREFIX>_EVENTS (per action) wins over the shared NOTIFY_EVENTS
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

            // warn about missing required fields (the server saves it anyway, but it won't work)
            var rules = api.Call("GET", $"/api/integrations/rules/{provider}") as JsonObject;
            JsonNode? rule = rules?[provider] ?? rules?.FirstOrDefault().Value;
            if (rule == null) Console.WriteLine($"  ! {toggle}: unknown provider '{provider}' — check {prefix}_TYPE");
            else
            {
                var missing = new List<string>();
                foreach (var f in (rule["fields"] as JsonArray) ?? new JsonArray())
                    if (f?["required"]?.GetValue<bool>() == true && !config.ContainsKey(f?["id"]?.ToString() ?? "")) missing.Add(f?["id"]?.ToString() ?? "");
                if (missing.Count > 0)
                    Console.WriteLine($"  ! {toggle}: {prefix}_CONFIG is missing required fields: {string.Join(", ", missing)} — the integration is saved but will not work");
            }

            // reuse an existing integration with the same type + name
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

    static Obj? RunApiProcessor()
    {
        // A run action needs category="run" and type="http"|"command"|"script", and every
        // parameter goes under `config` (url / method / body / deviceId / events / waitForResult).
        if (!EnvBool("ON_RUN_API")) return null;
        var action = Env("RUN_API_TYPE", "http")!.ToLowerInvariant();
        if (action == "https") action = "http";
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

        // Run On: which device runs the action (omit -> the transfer's device)
        var device = Env("RUN_API_DEVICE", "")!.ToLowerInvariant();
        if (device == "source") config["deviceId"] = Require("SOURCE_ID");
        else if (device.Length > 0) config["deviceId"] = Env("RUN_API_DEVICE");

        // config.events = {"completed": true, ...}; omit to fire on every event.
        var events = new Obj();
        foreach (var e in EnvList("RUN_API_EVENTS")) events[e] = true;
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

    // ============================================================== when it runs
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
    // first future time matching the cadence + clock, in SCHEDULE_TZ
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
    // REPEAT_START_AT wins; else REPEAT_RUN_NOW=true also runs once now;
    // default: no immediate run, the first run is the next occurrence.
    static Obj StartFields(string every, HashSet<int> weekdays, HashSet<int> doms)
    {
        var startAt = Env("REPEAT_START_AT");
        if (startAt != null) return Ob("startDateType", "specific", "startDate", startAt);
        if (EnvBool("REPEAT_RUN_NOW", false)) return Ob("startDateType", "now", "startDate", NowIso());
        var first = NextOccurrence(every, weekdays, doms).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        Console.WriteLine($"  first run at {first} ({Env("SCHEDULE_TZ", "Asia/Seoul")}); no immediate run");
        return Ob("startDateType", "specific", "startDate", first);
    }

    // Recurring: schedule type hour | day | week | month.
    static Obj BuildSchedule()
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
                var day = new[] { "lastday", "last" }.Contains(d.Trim().ToLowerInvariant()) ? "0" : d;   // "Last day" -> 0
                if (!day.All(char.IsDigit) || int.Parse(day) < 0 || int.Parse(day) > 31) Fail($"REPEAT_DAY_OF_MONTH: invalid day '{d}' (1-31, or 'last' = last day)");
                dates.Add(int.Parse(day).ToString()); doms.Add(int.Parse(day));
            }
            sched["dayInMonth"] = dates;
        }
        foreach (var kv in StartFields(every, weekdays, doms)) sched[kv.Key] = kv.Value;
        return sched;
    }

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

    static Obj AutomationBody(string name, List<string[]> pairs, Obj options, bool integrity, bool isDir, Obj schedule, Arr processors)
    {
        var details = new Arr();
        foreach (var p in pairs) details.Add(AutomationDetail(p, options, integrity, isDir));
        var body = Ob("name", name, "flowName", name, "transferType", "normal", "timezone", Env("SCHEDULE_TZ", "Asia/Seoul"),
            "details", details, "schedules", Ar(schedule), "step", 1, "isUpcoming", false);
        if (processors.Count > 0) body["processors"] = processors;
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
        var name = Env("NAME") ?? "send-many-recurring-follow-up";
        var (options, integrity) = FileOptions();
        var processors = AfterTransferProcessors();

        var api = new Api();
        if (options.ContainsKey(SAVEPATH_DEVICE_FLAG)) LoadDeviceNames(api);
        Console.WriteLine($"[send-many-recurring-follow-up]  {pairs.Count} device pair(s)");

        try
        {
            if (INTEGRATIONS.Any(r => EnvBool(r[0]))) { Console.WriteLine("integrations:"); EnsureIntegrations(api); }

            var body = AutomationBody(name, pairs, options, integrity, isDir, BuildSchedule(), processors);

            var automationId = api.Call("POST", "/api/automations", body)?["automationId"]?.ToString();
            Console.WriteLine($"automation created: {automationId}");
            var s = new Obj((Obj)((Arr)body["schedules"]!)[0]!); s.Remove("startDate"); s.Remove("startDateType");
            Console.WriteLine($"  repeat: every {Env("REPEAT_EVERY", "day")} {ToJson(s)}");
            return 0;
        }
        catch (ApiError e) { Fail($"API error {e.Status}: {e.Message}"); return 1; }
        catch (Exception e) { Fail(e.Message); return 1; }
    }
}
