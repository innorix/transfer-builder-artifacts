// =============================================================================
//  INNORIX Transfer Builder example — Collect Matching Files from Many Devices Right Now With Follow-Up Actions
// -----------------------------------------------------------------------------
//  Result page : https://innorix.com/en/builder/collect-matching-many-now-follow-up
//  Devices     : several source devices (SOURCE_IDS) -> one target device
//  Starts      : right now (the program waits for the run and prints the result)
//  Files       : only files that match FILTER_EXT / FILTER_MIN_MB / FILTER_MAX_MB
//  After       : follow-up actions (Run API, Monitoring, Message, Virus Scan, Email)
//
//  Every setting lives in the .env file next to this program (see .env.builder.example).
//  Requirements:  Java 11+ (uses the built-in java.net.http, no external dependencies)
//  Run (Java 11+ single-file launch):
//    java ComboBuilder.java                 # use the .env in this folder
//    java ComboBuilder.java path/to/x.env   # or point it at another .env
//  Or:  javac ComboBuilder.java && java ComboBuilder
// =============================================================================

import java.net.URI;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.time.ZoneId;
import java.util.*;

public class ComboBuilder {

    // ============================================================== .env & env
    static final Map<String, String> ENV = new HashMap<>();

    static void loadEnvFile(String file) {
        java.io.File f = new java.io.File(file);
        if (!f.exists()) { System.out.println("note: " + file + " not found, using the current environment only"); return; }
        try (java.io.BufferedReader br = new java.io.BufferedReader(
                new java.io.InputStreamReader(new java.io.FileInputStream(f), StandardCharsets.UTF_8))) {
            String raw;
            while ((raw = br.readLine()) != null) {
                String line = raw.trim();
                int eq = line.indexOf('=');
                if (line.isEmpty() || line.startsWith("#") || eq < 0) continue;
                String key = line.substring(0, eq).trim();
                String value = line.substring(eq + 1).trim();
                char c0 = value.isEmpty() ? ' ' : value.charAt(0);
                if (c0 != '"' && c0 != '\'') {
                    if (value.startsWith("#")) value = "";
                    else { int h = value.indexOf(" #"); if (h >= 0) value = value.substring(0, h).stripTrailing(); }
                } else if (value.length() >= 2 && value.charAt(value.length() - 1) == c0) {
                    value = value.substring(1, value.length() - 1);
                }
                if (!ENV.containsKey(key) && System.getenv(key) == null) ENV.put(key, value);
            }
        } catch (Exception e) { fail("cannot read " + file + ": " + e.getMessage()); }
    }

    static String env(String key, String dflt) {
        String v = System.getenv(key);
        if (v == null) v = ENV.get(key);
        return (v == null || v.trim().isEmpty()) ? dflt : v.trim();
    }
    static String env(String key) { return env(key, null); }
    static boolean envBool(String key, boolean dflt) {
        String v = env(key);
        return v == null ? dflt : Arrays.asList("1", "true", "yes", "on").contains(v.toLowerCase());
    }
    static int envInt(String key, int dflt) {
        String v = env(key);
        if (v == null) return dflt;
        try { return Integer.parseInt(v); } catch (Exception e) { fail(key + " must be an integer, got: " + v); return 0; }
    }
    static Double envDouble(String key) {
        String v = env(key);
        if (v == null) return null;
        try { return Double.parseDouble(v); } catch (Exception e) { fail(key + " must be a number, got: " + v); return null; }
    }
    static List<String> envList(String key) {
        List<String> out = new ArrayList<>();
        String v = env(key);
        if (v != null) for (String p : v.split(",")) if (!p.trim().isEmpty()) out.add(p.trim());
        return out;
    }
    @SuppressWarnings("unchecked")
    static Map<String, Object> envJson(String key) {
        String v = env(key);
        if (v == null) return new LinkedHashMap<>();
        Object o = Json.parse(v);
        if (!(o instanceof Map)) fail(key + " must be a JSON object");
        return (Map<String, Object>) o;
    }
    static String require(String key) {
        String v = env(key);
        if (v == null) fail("missing required setting in .env: " + key);
        return v;
    }
    static void fail(String msg) { System.err.println(msg); System.exit(1); }

    // small builders
    static Map<String, Object> obj(Object... kv) {
        Map<String, Object> m = new LinkedHashMap<>();
        for (int i = 0; i < kv.length; i += 2) m.put((String) kv[i], kv[i + 1]);
        return m;
    }
    static List<Object> arr(Object... items) { return new ArrayList<>(Arrays.asList(items)); }

    // ============================================================== JSON
    static final class Json {
        static String write(Object o) { StringBuilder sb = new StringBuilder(); w(o, sb); return sb.toString(); }

        @SuppressWarnings("unchecked")
        static void w(Object o, StringBuilder sb) {
            if (o == null) { sb.append("null"); }
            else if (o instanceof String) { str((String) o, sb); }
            else if (o instanceof Boolean || o instanceof Integer || o instanceof Long) { sb.append(o.toString()); }
            else if (o instanceof Double) { double d = (Double) o; if (d == Math.rint(d) && !Double.isInfinite(d)) sb.append((long) d); else sb.append(d); }
            else if (o instanceof Map) {
                sb.append('{'); boolean first = true;
                for (Map.Entry<String, Object> e : ((Map<String, Object>) o).entrySet()) {
                    if (!first) sb.append(','); first = false;
                    str(e.getKey(), sb); sb.append(':'); w(e.getValue(), sb);
                }
                sb.append('}');
            } else if (o instanceof List) {
                sb.append('['); boolean first = true;
                for (Object e : (List<Object>) o) { if (!first) sb.append(','); first = false; w(e, sb); }
                sb.append(']');
            } else { str(o.toString(), sb); }
        }
        static void str(String s, StringBuilder sb) {
            sb.append('"');
            for (int i = 0; i < s.length(); i++) {
                char c = s.charAt(i);
                switch (c) {
                    case '"': sb.append("\\\""); break;
                    case '\\': sb.append("\\\\"); break;
                    case '\n': sb.append("\\n"); break;
                    case '\r': sb.append("\\r"); break;
                    case '\t': sb.append("\\t"); break;
                    default: if (c < 0x20) sb.append(String.format("\\u%04x", (int) c)); else sb.append(c);
                }
            }
            sb.append('"');
        }

        // minimal recursive-descent parser -> Map/List/String/Double/Boolean/null
        private final String s; private int i;
        private Json(String s) { this.s = s; }
        static Object parse(String s) { Json p = new Json(s); p.ws(); Object v = p.val(); return v; }
        private void ws() { while (i < s.length() && Character.isWhitespace(s.charAt(i))) i++; }
        private Object val() {
            ws(); char c = s.charAt(i);
            if (c == '{') return objp();
            if (c == '[') return arrp();
            if (c == '"') return strp();
            if (c == 't') { i += 4; return Boolean.TRUE; }
            if (c == 'f') { i += 5; return Boolean.FALSE; }
            if (c == 'n') { i += 4; return null; }
            return nump();
        }
        private Map<String, Object> objp() {
            Map<String, Object> m = new LinkedHashMap<>(); i++; ws();
            if (s.charAt(i) == '}') { i++; return m; }
            while (true) {
                ws(); String k = strp(); ws(); i++; /* ':' */ Object v = val(); m.put(k, v); ws();
                char c = s.charAt(i++); if (c == '}') break; /* ',' -> continue */
            }
            return m;
        }
        private List<Object> arrp() {
            List<Object> a = new ArrayList<>(); i++; ws();
            if (s.charAt(i) == ']') { i++; return a; }
            while (true) {
                a.add(val()); ws(); char c = s.charAt(i++); if (c == ']') break;
            }
            return a;
        }
        private String strp() {
            StringBuilder sb = new StringBuilder(); i++; /* opening quote */
            while (true) {
                char c = s.charAt(i++);
                if (c == '"') break;
                if (c == '\\') {
                    char e = s.charAt(i++);
                    switch (e) {
                        case 'n': sb.append('\n'); break; case 't': sb.append('\t'); break;
                        case 'r': sb.append('\r'); break; case 'b': sb.append('\b'); break;
                        case 'f': sb.append('\f'); break; case '/': sb.append('/'); break;
                        case '"': sb.append('"'); break; case '\\': sb.append('\\'); break;
                        case 'u': sb.append((char) Integer.parseInt(s.substring(i, i + 4), 16)); i += 4; break;
                        default: sb.append(e);
                    }
                } else sb.append(c);
            }
            return sb.toString();
        }
        private Double nump() {
            int start = i;
            while (i < s.length() && "+-0123456789.eE".indexOf(s.charAt(i)) >= 0) i++;
            return Double.parseDouble(s.substring(start, i));
        }
    }

    // response accessors
    @SuppressWarnings("unchecked")
    static Map<String, Object> asMap(Object o) { return o instanceof Map ? (Map<String, Object>) o : new LinkedHashMap<>(); }
    @SuppressWarnings("unchecked")
    static List<Object> asList(Object o) { return o instanceof List ? (List<Object>) o : new ArrayList<>(); }
    static String asStr(Object o) { return o == null ? null : o.toString(); }
    static Integer asInt(Object o) { return o instanceof Number ? ((Number) o).intValue() : null; }

    // ============================================================== API client
    static final class ApiError extends RuntimeException {
        final int status; ApiError(int status, String msg) { super(msg); this.status = status; }
    }

    static final class Api {
        final String base; final int timeout; final String workspace; final boolean debug;
        String apiKey; String access; String refresh;
        final HttpClient http = HttpClient.newHttpClient();

        Api() {
            base = env("INNORIX_BASE_URL", "https://app.innorix.com").replaceAll("/+$", "");
            timeout = envInt("INNORIX_TIMEOUT", 30);
            workspace = env("INNORIX_WORKSPACE_ID");
            debug = envBool("DEBUG", false);   // DEBUG=true -> print every request + response
            apiKey = env("INNORIX_API_KEY");    // -> x-api-key
            access = env("INNORIX_ACCESS_TOKEN"); // -> Authorization: Bearer
            if (apiKey == null && access == null) {
                String email = env("INNORIX_EMAIL"), password = env("INNORIX_PASSWORD");
                if (email == null || password == null)
                    fail("set INNORIX_API_KEY or INNORIX_ACCESS_TOKEN, or INNORIX_EMAIL and INNORIX_PASSWORD in .env");
                login(email, password);
            }
        }

        HttpResponse<String> send(String method, String url, Map<String, String> headers, Object body) {
            try {
                HttpRequest.Builder b = HttpRequest.newBuilder(URI.create(url)).timeout(Duration.ofSeconds(timeout));
                headers.forEach(b::header);
                HttpRequest.BodyPublisher bp = body == null
                        ? HttpRequest.BodyPublishers.noBody()
                        : HttpRequest.BodyPublishers.ofString(Json.write(body), StandardCharsets.UTF_8);
                b.method(method, bp);
                return http.send(b.build(), HttpResponse.BodyHandlers.ofString());
            } catch (Exception e) { throw new ApiError(0, "cannot reach " + base + ": " + e.getClass().getSimpleName()); }
        }

        void login(String email, String password) {
            Map<String, String> h = new HashMap<>(); h.put("Content-Type", "application/json");
            HttpResponse<String> r = send("POST", base + "/api/auth/login", h, obj("email", email, "password", password));
            if (r.statusCode() / 100 != 2) fail("login failed (" + r.statusCode() + "): " + snippet(r.body(), 200));
            Map<String, Object> user = asMap(asMap(asMap(Json.parse(r.body())).get("data")).get("user"));
            access = asStr(user.get("accessToken")); refresh = asStr(user.get("refreshToken"));
        }

        boolean renew() {
            if (refresh == null) return false;
            Map<String, String> h = new HashMap<>(); h.put("X-Refresh-Token", refresh);
            HttpResponse<String> r = send("POST", base + "/api/auth/token/refresh", h, null);
            if (r.statusCode() / 100 != 2) return false;
            Map<String, Object> data = asMap(asMap(Json.parse(r.body())).get("data"));
            access = asStr(data.get("accessToken"));
            if (data.get("refreshToken") != null) refresh = asStr(data.get("refreshToken"));
            return true;
        }

        Object call(String method, String path, Object body, Map<String, String> params) { return call(method, path, body, params, false); }
        Object call(String method, String path) { return call(method, path, null, null, false); }

        Object call(String method, String path, Object body, Map<String, String> params, boolean retried) {
            Map<String, String> headers = new HashMap<>();
            headers.put("Content-Type", "application/json");
            if (apiKey != null) headers.put("x-api-key", apiKey);
            else headers.put("Authorization", "Bearer " + access);
            if (workspace != null) headers.put("x-workspace-id", workspace);
            String url = base + path;
            if (params != null && !params.isEmpty()) {
                StringBuilder q = new StringBuilder();
                for (Map.Entry<String, String> e : params.entrySet()) {
                    q.append(q.length() == 0 ? "?" : "&")
                     .append(URLEncoder.encode(e.getKey(), StandardCharsets.UTF_8)).append('=')
                     .append(URLEncoder.encode(e.getValue(), StandardCharsets.UTF_8));
                }
                url += q;
            }
            if (debug) {
                System.out.println("\n>> " + method + " " + url);
                Map<String, String> safe = new HashMap<>(headers);
                if (safe.containsKey("x-api-key")) safe.put("x-api-key", "***");
                if (safe.containsKey("Authorization")) safe.put("Authorization", "***");
                System.out.println("   headers: " + safe);
                if (body != null) System.out.println("   body   : " + Json.write(body));
            }
            HttpResponse<String> r = send(method, url, headers, body);
            if (debug) System.out.println("<< " + r.statusCode() + "\n   response: "
                    + (r.body() == null || r.body().isEmpty() ? "(empty)" : r.body()));
            if (r.statusCode() == 401 && !retried && renew()) return call(method, path, body, params, true);
            Object payload = r.body() == null || r.body().isEmpty() ? new LinkedHashMap<>() : Json.parse(r.body());
            Map<String, Object> pm = asMap(payload);
            if (r.statusCode() / 100 != 2) {
                // Surface the full response body, not just the generic status text.
                Object msg = pm.getOrDefault("message", pm.get("error"));
                String detail = msg != null ? msg.toString() : snippet(r.body(), 500);
                if (r.body() != null && !r.body().isEmpty()) detail += "  |  full response: " + r.body();
                throw new ApiError(r.statusCode(), detail);
            }
            return pm.get("data");
        }
    }

    static String snippet(String s, int n) { return s == null ? "" : (s.length() > n ? s.substring(0, n) : s); }

    // Automation details use plain absolute paths (forward slashes) in
    // sourceItem[].filePath and targetPath — not the {deviceId}_ino_{base64} token.
    static String plainPath(String rawPath) {
        return (rawPath == null ? "" : rawPath).replace("\\", "/");
    }
    static String nowIso() {
        return java.time.format.DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'")
                .format(java.time.OffsetDateTime.now(ZoneId.of("UTC")));
    }

    // ============================================================== devices
    // Line a *_PATHS list up with its *_IDS list: 1 path = same for all, N = one each.
    static List<String> expandPaths(List<String> paths, int count, String pathsKey, String idsKey) {
        if (paths.isEmpty()) fail(pathsKey + " is required (one path, or one per " + idsKey + ")");
        if (paths.size() == 1) { List<String> out = new ArrayList<>(); for (int i = 0; i < count; i++) out.add(paths.get(0)); return out; }
        if (paths.size() == count) return paths;
        fail(pathsKey + " must have 1 entry (same for all) or exactly " + count + " (one per " + idsKey + ")");
        return paths;   // unreachable (fail exits)
    }

    // Every device in SOURCE_IDS (+ SOURCE_PATHS) -> one target (TARGET_ID + TARGET_PATH).
    static List<String[]> devicePairs() {
        String target = require("TARGET_ID"), targetPath = require("TARGET_PATH");
        List<String> sources = envList("SOURCE_IDS");
        if (sources.isEmpty()) fail("missing required setting in .env: SOURCE_IDS");
        List<String> paths = expandPaths(envList("SOURCE_PATHS"), sources.size(), "SOURCE_PATHS", "SOURCE_IDS");
        List<String[]> pairs = new ArrayList<>();
        for (int i = 0; i < sources.size(); i++)
            pairs.add(new String[]{sources.get(i), paths.get(i), target, targetPath});
        return pairs;
    }

    // ============================================================== file options
    static final Map<String, String> DUPLICATE_MAP = Map.of("skip", "nosend", "overwrite", "overwrite", "rename", "numbering");

    // SAVE_PATH=device marks the options map here; automationDetail() replaces it with the
    // per-pair source device name (the marker never reaches the request body).
    static final String SAVEPATH_DEVICE_FLAG = "__savepathDevice";
    static final Map<String, String> DEVICE_NAMES = new LinkedHashMap<>();

    static Object[] fileOptions() {
        // Duplicated Name — what to do when the target already has a file with the same name
        String raw = env("DUPLICATE_ACTION", "overwrite").toLowerCase();
        String action = DUPLICATE_MAP.get(raw);
        if (action == null) fail("DUPLICATE_ACTION must be overwrite|rename|skip, got: " + raw);
        Map<String, Object> options = obj("target-action", action);

        // Matching files only — Type Filter (extensions, allow/deny) + Size Filter (MB)
        Map<String, Object> fo = new LinkedHashMap<>();
        List<String> exts = envList("FILTER_EXT");
        if (!exts.isEmpty()) {
            boolean allow = !env("FILTER_MODE", "allow").toLowerCase().equals("deny");
            List<Object> clean = new ArrayList<>();
            for (String e : exts) clean.add(e.replaceAll("^\\.+", "").toLowerCase());
            fo.put("extension", obj("extension", clean, "allow", allow));
        }
        Double minMb = envDouble("FILTER_MIN_MB"), maxMb = envDouble("FILTER_MAX_MB");
        if (minMb != null) {
            fo.put("fileSize", obj("size", (long) (minMb * 1024 * 1024), "over", true, "equal", true));
            if (maxMb != null) System.out.println("  WARNING: both size bounds set, but the API's fileSize filter takes ONE bound — only FILTER_MIN_MB ("
                    + minMb + "MB) is applied and FILTER_MAX_MB (" + maxMb + "MB) is IGNORED, so larger files will be transferred");
        } else if (maxMb != null) {
            fo.put("fileSize", obj("size", (long) (maxMb * 1024 * 1024), "over", false, "equal", true));
        }
        if (!fo.isEmpty()) options.put("send-fileoption", fo);

        // Save Path: `savepath` carries the TARGET SUBFOLDER NAME as a string; `optionPath`
        // only records which mode produced it (1=run date, 2=device name). The source
        // folder structure is preserved under the target path either way.
        String save = env("SAVE_PATH", "root").toLowerCase();
        if (save.equals("root") || save.equals("false")) { /* no subfolder */ }
        else if (save.equals("date")) { options.put("savepath", runDateFolder()); options.put("optionPath", 1); }
        else if (save.equals("device")) { options.put("optionPath", 2); options.put(SAVEPATH_DEVICE_FLAG, true); }
        else fail("SAVE_PATH must be root|date|device, got: " + save);
        return new Object[]{options, envBool("INTEGRITY", false)};
    }

    static String runDateFolder() {
        return new java.text.SimpleDateFormat(env("SAVE_PATH_DATE_FORMAT", "yyMMdd")).format(new java.util.Date());
    }

    static void loadDeviceNames(Api api) {
        // deviceId -> device name (GET /api/devices, paginated): savepath must carry the name
        for (int page = 1; page <= 20; page++) {
            Map<String, String> params = new LinkedHashMap<>();
            params.put("page", String.valueOf(page));
            params.put("size", "100");
            Map<String, Object> result = asMap(api.call("GET", "/api/devices", null, params));
            Object devices = result.get("devices");
            if (devices instanceof List) {
                for (Object d : (List<?>) devices) {
                    Map<String, Object> device = asMap(d);
                    String id = asStr(device.get("deviceId"));
                    if (id != null && !id.isEmpty()) {
                        String deviceName = asStr(device.get("name"));
                        DEVICE_NAMES.put(id, (deviceName == null || deviceName.isEmpty()) ? id : deviceName);
                    }
                }
            }
            Object last = result.get("last_page") != null ? result.get("last_page") : result.get("lastPage");
            int lastPage = (last instanceof Number) ? ((Number) last).intValue() : 1;
            if (page >= lastPage) return;
        }
    }

    // ============================================================== after transfer (follow-up actions)
    // Message / Virus Scan / Email are workspace-wide integrations (POST /api/integrations)
    // that fire for the events in NOTIFY_EVENTS. Run API and Monitoring are processors
    // attached to this automation (see afterTransferProcessors()).
    static final String[][] INTEGRATIONS = {
            {"ON_MESSAGE", "notification", "MESSAGE"},
            {"ON_VIRUS_SCAN", "security", "VIRUS_SCAN"},
            {"ON_EMAIL", "email", "EMAIL"},
    };

    // <PREFIX>_EVENTS (per action) wins over the shared NOTIFY_EVENTS
    static Map<String, Object> notifyEvents(String prefix) {
        List<String> perAction = prefix == null ? new ArrayList<>() : envList(prefix + "_EVENTS");
        List<String> evs = !perAction.isEmpty() ? perAction
                : envList("NOTIFY_EVENTS").isEmpty() ? Arrays.asList("completed", "error") : envList("NOTIFY_EVENTS");
        Map<String, Object> m = new LinkedHashMap<>();
        for (String e : evs) m.put(e, true);
        return m;
    }

    static void ensureIntegrations(Api api) {
        for (String[] row : INTEGRATIONS) {
            String toggle = row[0], category = row[1], prefix = row[2];
            if (!envBool(toggle, false)) continue;
            Map<String, Object> events = notifyEvents(prefix);
            String provider = require(prefix + "_TYPE").toLowerCase();
            Map<String, Object> config = envJson(prefix + "_CONFIG");
            String name = env(prefix + "_NAME");
            if (name == null) name = config.get("name") != null ? config.get("name").toString() : "builder-" + provider;
            config.putIfAbsent("name", name);

            // warn about missing required fields (the server saves it anyway, but it won't work)
            Map<String, Object> rules = asMap(api.call("GET", "/api/integrations/rules/" + provider));
            Object rule = rules.get(provider);
            if (rule == null && !rules.isEmpty()) rule = rules.values().iterator().next();
            if (rule == null) System.out.println("  ! " + toggle + ": unknown provider '" + provider + "' — check " + prefix + "_TYPE");
            else {
                List<String> missing = new ArrayList<>();
                for (Object f : asList(asMap(rule).get("fields"))) {
                    Map<String, Object> fm = asMap(f);
                    if (Boolean.TRUE.equals(fm.get("required")) && !config.containsKey(asStr(fm.get("id")))) missing.add(asStr(fm.get("id")));
                }
                if (!missing.isEmpty()) System.out.println("  ! " + toggle + ": " + prefix + "_CONFIG is missing required fields: "
                        + String.join(", ", missing) + " — the integration is saved but will not work");
            }

            // reuse an existing integration with the same type + name
            Map<String, String> q = new HashMap<>(); q.put("category", category);
            boolean reused = false;
            for (Object it : asList(api.call("GET", "/api/integrations", null, q))) {
                Map<String, Object> im = asMap(it);
                if (provider.equals(asStr(im.get("type"))) && name.equals(asStr(im.get("name")))) {
                    System.out.println("  " + toggle + ": " + category + "/" + provider + " '" + name + "' already exists, reused");
                    reused = true; break;
                }
            }
            if (reused) continue;
            Map<String, Object> reqBody = obj("name", name, "type", provider, "category", category, "config", config,
                    "notificationConfig", obj("events", events));
            for (String k : new String[]{"webhookUrl", "baseUrl", "mode", "authType"}) if (config.containsKey(k)) reqBody.put(k, config.get(k));
            if (config.containsKey("webhookUrl")) reqBody.putIfAbsent("mode", "webhook");
            Map<String, Object> result = asMap(api.call("POST", "/api/integrations", reqBody, null));
            Object id = result.getOrDefault("integrationId", result.get("id"));
            System.out.println("  " + toggle + ": " + category + "/" + provider + " '" + name + "' created (" + id + ")");
        }
    }

    static Map<String, Object> runApiProcessor() {
        // A run action needs category="run" and type="http"|"command"|"script", and every
        // parameter goes under `config` (url / method / body / deviceId / events / waitForResult).
        if (!envBool("ON_RUN_API", false)) return null;
        String action = env("RUN_API_TYPE", "http").toLowerCase();
        if (action.equals("https")) action = "http";
        if (!Arrays.asList("http", "command", "script").contains(action))
            fail("RUN_API_TYPE must be http|command|script, got: " + action);

        Map<String, Object> config = new LinkedHashMap<>();
        if (action.equals("http")) {
            config.put("url", require("RUN_API_URL"));
            config.put("method", env("RUN_API_METHOD", "POST").toUpperCase());
            String body = env("RUN_API_BODY");
            if (body != null && !body.isEmpty()) {
                Object parsed = Json.parse(body);
                if (!(parsed instanceof Map)) fail("RUN_API_BODY must be a JSON object, e.g. {\"key\":\"value\"}");
                config.put("body", parsed);
            }
        } else if (action.equals("command")) {
            config.put("command", require("RUN_API_COMMAND"));
        } else {
            config.put("file", require("RUN_API_SCRIPT"));
            String args = env("RUN_API_ARGS");
            if (args != null && !args.isEmpty()) config.put("args", args);
        }

        // Run On: which device runs the action (omit -> the transfer's device)
        String device = env("RUN_API_DEVICE", "").toLowerCase();
        if (device.equals("target")) config.put("deviceId", require("TARGET_ID"));
        else if (!device.isEmpty()) config.put("deviceId", env("RUN_API_DEVICE"));

        // config.events = {"completed": true, ...}; omit to fire on every event.
        Map<String, Object> events = new LinkedHashMap<>();
        for (String e : envList("RUN_API_EVENTS")) events.put(e, true);
        if (!events.isEmpty()) config.put("events", events);
        if (envBool("RUN_API_WAIT", false)) config.put("waitForResult", true);

        return obj("category", "run", "type", action,
                "name", env("RUN_API_NAME", "builder-run-api"), "config", config);
    }

    static Map<String, Object> monitoringProcessor() {
        if (!envBool("ON_MONITORING", false)) return null;
        String provider = require("MONITORING_TYPE").toLowerCase();
        Map<String, Object> config = envJson("MONITORING_CONFIG");
        String name = env("MONITORING_NAME");
        if (name == null) name = config.get("name") != null ? config.get("name").toString() : "builder-" + provider;
        config.putIfAbsent("name", name);
        return obj("category", "monitoring", "type", provider, "name", name, "config", config,
                "notificationConfig", obj("events", notifyEvents("MONITORING")));
    }

    static List<Object> afterTransferProcessors() {
        List<Object> ps = new ArrayList<>();
        Map<String, Object> r = runApiProcessor(); if (r != null) ps.add(r);
        Map<String, Object> m = monitoringProcessor(); if (m != null) ps.add(m);
        return ps;
    }

    // ============================================================== when it runs
    // Right now: an automation whose schedule is type=none, startDateType=now runs at once.
    static Map<String, Object> buildSchedule() {
        return obj("type", "none", "startDateType", "now", "startDate", nowIso(), "timezone", env("SCHEDULE_TZ", "Asia/Seoul"));
    }

    // ============================================================== automation body
    static Map<String, Object> automationDetail(String[] pair, Map<String, Object> options, boolean integrity, boolean isDir) {
        String source = pair[0], sourcePath = pair[1], target = pair[2], targetPath = pair[3];
        Map<String, Object> detailOptions = new LinkedHashMap<>(options);
        if (Boolean.TRUE.equals(detailOptions.remove(SAVEPATH_DEVICE_FLAG))) {
            // subfolder per SOURCE device
            detailOptions.put("savepath", DEVICE_NAMES.getOrDefault(source, source));
        }
        if (integrity) detailOptions.put("checkIntegrity", true);
        // Plain absolute paths: sourceItem[].filePath for the source, targetPath for the
        // destination (both forward-slashed). targetPath must not be "/" or empty.
        Map<String, Object> item = obj("filePath", plainPath(sourcePath), "isDir", isDir);
        Map<String, Object> to = new LinkedHashMap<>(); to.put("noSchedule", false); to.putAll(detailOptions);
        return obj("senderId", source, "receiverId", target, "sourceItem", arr(item),
                "targetPath", plainPath(targetPath), "step", 1, "transferOptions", to);
    }

    static Map<String, Object> automationBody(String name, List<String[]> pairs, Map<String, Object> options,
            boolean integrity, boolean isDir, Map<String, Object> schedule, List<Object> processors) {
        List<Object> details = new ArrayList<>();
        for (String[] p : pairs) details.add(automationDetail(p, options, integrity, isDir));
        Map<String, Object> body = obj("name", name, "flowName", name, "transferType", "normal",
                "timezone", env("SCHEDULE_TZ", "Asia/Seoul"), "details", details, "schedules", arr(schedule),
                "step", 1, "isUpcoming", false);
        if (!processors.isEmpty()) body.put("processors", processors);
        return body;
    }

    // ============================================================== wait for the run
    static final Set<Integer> TERMINAL = new HashSet<>(Arrays.asList(2, 4, 5, 9, 99));
    static final Map<Integer, String> STATUS = new HashMap<>();
    static {
        int[] k = {-1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 99};
        String[] v = {"queued", "waiting", "started", "complete", "paused", "error", "cancelled",
                "transferring", "skipped", "retry", "partial-complete", "retry-checking",
                "virus-scanning", "syncing", "incoming", "activity", "fail"};
        for (int j = 0; j < k.length; j++) STATUS.put(k[j], v[j]);
    }

    static Map<String, Object> waitFor(Api api, String monitorId, int timeoutSec) throws InterruptedException {
        // Poll GET /api/transfers/{monitorId} until terminal; print on change + heartbeat.
        long start = System.currentTimeMillis(), lastBeat = start, lastChange = start;
        String lastState = null;
        while ((System.currentTimeMillis() - start) / 1000 < timeoutSec) {
            Map<String, Object> detail = asMap(api.call("GET", "/api/transfers/" + monitorId));
            Integer status = asInt(detail.get("status"));
            Object percent = detail.getOrDefault("percent", detail.getOrDefault("progress", 0));
            long now = System.currentTimeMillis();
            String state = status + "|" + percent;
            boolean changed = !state.equals(lastState);
            if (changed) { lastState = state; lastChange = now; }
            if (changed || now - lastBeat >= 15000) {
                long stalled = (now - lastChange) / 1000;
                String note = stalled >= 60 ? "  — no progress for " + stalled + "s; check the target agent/connection" : "";
                System.out.println("    status: " + STATUS.getOrDefault(status, String.valueOf(status)) + " (" + percent + "%)  ["
                        + ((now - start) / 1000) + "s elapsed]" + note);
                lastBeat = now;
            }
            Object term = detail.get("isTerminal");
            boolean isTerminal = term instanceof Boolean ? (Boolean) term : TERMINAL.contains(status);
            if (isTerminal) return detail;
            Thread.sleep(3000);
        }
        throw new RuntimeException("transfer " + monitorId + " did not finish within " + timeoutSec
                + "s (still running — raise WAIT_TIMEOUT, or set WAIT=false to not wait)");
    }

    // Non-transfer summary row kinds in the active list — skip these; a real transfer
    // row has type "monitor" (its top-level monitorId is the id to poll).
    static final Set<String> SKIP_ROW_TYPES = new HashSet<>(Arrays.asList("automation", "history", "flow"));

    static List<String> activeMonitorIds(Api api, String automationId) {
        // monitorIds of this automation's active transfers (GET /api/transfers?automationId=...).
        // data = { data: [records], pagination: {...} }; a transfer row is { type: "monitor", monitorId, ... }
        Map<String, String> params = new HashMap<>();
        params.put("automationId", automationId);
        Map<String, Object> wrap = asMap(api.call("GET", "/api/transfers", null, params));
        List<String> ids = new ArrayList<>();
        for (Object rec : asList(wrap.get("data"))) {
            Map<String, Object> r = asMap(rec);
            Object type = r.get("type");
            if (type != null && SKIP_ROW_TYPES.contains(type.toString())) continue;
            Object m = r.get("monitorId") != null ? r.get("monitorId") : r.get("id");
            if (m != null) ids.add(m.toString());
        }
        return ids;
    }

    static List<String> resolveMonitorIds(Api api, String automationId, int expected, int appearWait) throws InterruptedException {
        // Poll until the run(s) start and collect their monitorIds (accumulated across polls,
        // so a short transfer that finishes between polls is not missed).
        long start = System.currentTimeMillis();
        List<String> seen = new ArrayList<>();
        while (true) {
            for (String mid : activeMonitorIds(api, automationId)) if (!seen.contains(mid)) seen.add(mid);
            if (seen.size() >= expected) return seen.subList(0, expected);
            if ((System.currentTimeMillis() - start) / 1000 >= appearWait) return seen;
            System.out.println("    waiting for the run to start... (" + seen.size() + "/" + expected + ") ["
                    + ((System.currentTimeMillis() - start) / 1000) + "s]");
            Thread.sleep(3000);
        }
    }

    // One transfer per device pair; returns the process exit code.
    static int waitForRun(Api api, String automationId, int expected) throws InterruptedException {
        if (!envBool("WAIT", true)) {
            System.out.println("  (WAIT=false) not waiting; find the transfer with GET /api/transfers?automationId=" + automationId);
            return 0;
        }
        System.out.println("  waiting for the run to start and complete (polling every 3s; set WAIT=false to skip)...");
        int appearWait = envInt("APPEAR_WAIT", 120);
        List<String> monitorIds = resolveMonitorIds(api, automationId, expected, appearWait);
        if (monitorIds.isEmpty()) {
            System.out.println("  ERROR: no transfer started within " + appearWait + "s — the automation was created "
                    + "but nothing ran. Check GET /api/transfers?automationId=" + automationId);
            return 1;
        }
        int missing = expected - monitorIds.size();
        if (missing > 0) {
            System.out.println("  ERROR: only " + monitorIds.size() + " of " + expected + " transfer(s) started within "
                    + appearWait + "s (raise APPEAR_WAIT if the devices are slow to pick the run up)");
        }
        System.out.println("  run started: " + monitorIds.size() + " transfer(s) — " + String.join(", ", monitorIds));
        int failed = 0;
        for (String monitorId : monitorIds) {
            Map<String, Object> detail = waitFor(api, monitorId, envInt("WAIT_TIMEOUT", 3600));
            Integer status = asInt(detail.get("status"));
            System.out.println("  finished " + monitorId + ": " + STATUS.getOrDefault(status, String.valueOf(status))
                    + " (" + detail.getOrDefault("fileCount", 0) + " files)");
            if (status == null || status != 2) failed++;
        }
        return (failed != 0 || missing > 0) ? 1 : 0;
    }

    // ============================================================== main
    public static void main(String[] args) throws Exception {
        // Prefer .env in the current working directory (run from this folder);
        // otherwise fall back to the folder the program lives in.
        String envPath = args.length > 0 ? args[0]
                : (new java.io.File(".env").exists() ? ".env" : new java.io.File(programDir(), ".env").getPath());
        loadEnvFile(envPath);

        List<String[]> pairs = devicePairs();
        boolean isDir = envBool("SOURCE_IS_DIR", true);
        String name = env("NAME", "collect-matching-many-now-follow-up");
        Object[] fo = fileOptions();
        @SuppressWarnings("unchecked") Map<String, Object> options = (Map<String, Object>) fo[0];
        boolean integrity = (Boolean) fo[1];
        List<Object> processors = afterTransferProcessors();

        Api api = new Api();
        if (Boolean.TRUE.equals(options.get(SAVEPATH_DEVICE_FLAG))) loadDeviceNames(api);
        System.out.println("[collect-matching-many-now-follow-up]  " + pairs.size() + " device pair(s)");

        try {
            boolean anyIntegration = false;
            for (String[] r : INTEGRATIONS) if (envBool(r[0], false)) anyIntegration = true;
            if (anyIntegration) { System.out.println("integrations:"); ensureIntegrations(api); }

            Map<String, Object> body = automationBody(name, pairs, options, integrity, isDir, buildSchedule(), processors);

            String automationId = asStr(asMap(api.call("POST", "/api/automations", body, null)).get("automationId"));
            System.out.println("automation created: " + automationId);
            System.exit(waitForRun(api, automationId, pairs.size()));
        } catch (ApiError e) {
            fail("API error " + e.status + ": " + e.getMessage());
        } catch (RuntimeException e) {
            fail(e.getMessage());
        }
    }

    static String programDir() {
        try {
            java.io.File loc = new java.io.File(
                    ComboBuilder.class.getProtectionDomain().getCodeSource().getLocation().toURI());
            // A compiled classpath entry is the classes DIRECTORY (use it as-is);
            // a packaged run is a .jar FILE (use its parent folder).
            return loc.isDirectory() ? loc.getPath() : (loc.getParent() != null ? loc.getParent() : ".");
        } catch (Exception e) { return "."; }
    }
}
