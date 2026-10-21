// =============================================================================
//  INNORIX Transfer Builder example — Send Files to One Device on a Recurring Schedule
// -----------------------------------------------------------------------------
//  Result page : https://innorix.com/en/builder/send-one-recurring
//  Devices     : one source device -> one target device
//  Starts      : on a recurring schedule (REPEAT_EVERY hour | day | week | month)
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
import java.time.ZonedDateTime;
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
    static List<String> envList(String key) {
        List<String> out = new ArrayList<>();
        String v = env(key);
        if (v != null) for (String p : v.split(",")) if (!p.trim().isEmpty()) out.add(p.trim());
        return out;
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
    static String asStr(Object o) { return o == null ? null : o.toString(); }

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
    // One source -> one target: a single device pair.
    static List<String[]> devicePairs() {
        String source = require("SOURCE_ID"), sourcePath = require("SOURCE_PATH");
        String target = require("TARGET_ID"), targetPath = require("TARGET_PATH");
        List<String[]> pairs = new ArrayList<>();
        pairs.add(new String[]{source, sourcePath, target, targetPath});
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

    // ============================================================== when it runs
    static final Map<String, String> WEEKDAY = Map.of("sun", "sunday", "mon", "monday", "tue", "tuesday",
            "wed", "wednesday", "thu", "thursday", "fri", "friday", "sat", "saturday");
    // Sunday=0..Saturday=6
    static final Map<String, Integer> WEEKDAY_INDEX = Map.of("sunday", 0, "monday", 1, "tuesday", 2,
            "wednesday", 3, "thursday", 4, "friday", 5, "saturday", 6);

    static Map<String, Object> clock() {
        return obj("hour", env("REPEAT_HOUR", "12"), "minute", env("REPEAT_MINUTE", "00"),
                "ampm", env("REPEAT_AMPM", "am").toLowerCase(), "timezone", env("SCHEDULE_TZ", "Asia/Seoul"));
    }
    static int hour24() {
        return Integer.parseInt(env("REPEAT_HOUR", "12")) % 12 + (env("REPEAT_AMPM", "am").toLowerCase().equals("pm") ? 12 : 0);
    }
    static ZonedDateTime nowInTz(String tz) {
        try { return ZonedDateTime.now(ZoneId.of(tz)); }
        catch (Exception e) { System.out.println("  note: timezone '" + tz + "' unavailable; computing first run in local time"); return ZonedDateTime.now(); }
    }
    // first future time matching the cadence + clock, in SCHEDULE_TZ
    static ZonedDateTime nextOccurrence(String every, Set<Integer> weekdays, Set<Integer> doms) {
        ZonedDateTime now = nowInTz(env("SCHEDULE_TZ", "Asia/Seoul"));
        if (every.equals("hour")) {
            ZonedDateTime top = now.withMinute(0).withSecond(0).withNano(0);
            return top.isAfter(now) ? top : top.plusHours(1);
        }
        ZonedDateTime base = now.withHour(hour24()).withMinute(Integer.parseInt(env("REPEAT_MINUTE", "00"))).withSecond(0).withNano(0);
        for (int i = 0; i < 400; i++) {
            ZonedDateTime cand = base.plusDays(i);
            if (!cand.isAfter(now)) continue;
            if (every.equals("day")) return cand;
            if (every.equals("week") && weekdays.contains(cand.getDayOfWeek().getValue() % 7)) return cand;   // Mon=1..Sun=7 -> %7 => Sun=0..Sat=6
            if (every.equals("month")) {
                int last = cand.toLocalDate().lengthOfMonth();
                if (doms.contains(cand.getDayOfMonth()) || (doms.contains(0) && cand.getDayOfMonth() == last)) return cand;
            }
        }
        return base;
    }
    // REPEAT_START_AT wins; else REPEAT_RUN_NOW=true also runs once now;
    // default: no immediate run, the first run is the next occurrence.
    static Map<String, Object> startFields(String every, Set<Integer> weekdays, Set<Integer> doms) {
        String startAt = env("REPEAT_START_AT");
        if (startAt != null) return obj("startDateType", "specific", "startDate", startAt);
        if (envBool("REPEAT_RUN_NOW", false)) return obj("startDateType", "now", "startDate", nowIso());
        String first = nextOccurrence(every, weekdays, doms)
                .format(java.time.format.DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ss"));
        System.out.println("  first run at " + first + " (" + env("SCHEDULE_TZ", "Asia/Seoul") + "); no immediate run");
        return obj("startDateType", "specific", "startDate", first);
    }

    // Recurring: schedule type hour | day | week | month.
    static Map<String, Object> buildSchedule() {
        String every = env("REPEAT_EVERY", "day").toLowerCase();
        if (!Arrays.asList("hour", "day", "week", "month").contains(every)) fail("REPEAT_EVERY must be hour|day|week|month, got: " + every);
        Set<Integer> weekdays = new HashSet<>(), doms = new HashSet<>();
        Map<String, Object> sched = new LinkedHashMap<>(); sched.put("type", every); sched.putAll(clock());
        if (every.equals("week")) {
            List<Object> names = new ArrayList<>();
            List<String> ws = envList("REPEAT_WEEKDAY").isEmpty() ? Arrays.asList("monday") : envList("REPEAT_WEEKDAY");
            for (String d : ws) {
                String key = d.toLowerCase().substring(0, Math.min(3, d.length()));
                if (!WEEKDAY.containsKey(key)) fail("REPEAT_WEEKDAY: invalid weekday '" + d + "' (sunday..saturday)");
                names.add(WEEKDAY.get(key)); weekdays.add(WEEKDAY_INDEX.get(WEEKDAY.get(key)));
            }
            sched.put("dayInWeek", names);
        } else if (every.equals("month")) {
            List<Object> dates = new ArrayList<>();
            List<String> ds = envList("REPEAT_DAY_OF_MONTH").isEmpty() ? Arrays.asList("1") : envList("REPEAT_DAY_OF_MONTH");
            for (String d : ds) {
                String day = Arrays.asList("lastday", "last").contains(d.trim().toLowerCase()) ? "0" : d;   // "Last day" -> 0
                if (!day.matches("\\d+") || Integer.parseInt(day) < 0 || Integer.parseInt(day) > 31)
                    fail("REPEAT_DAY_OF_MONTH: invalid day '" + d + "' (1-31, or 'last' = last day)");
                dates.add(String.valueOf(Integer.parseInt(day))); doms.add(Integer.parseInt(day));
            }
            sched.put("dayInMonth", dates);
        }
        sched.putAll(startFields(every, weekdays, doms));
        return sched;
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
            boolean integrity, boolean isDir, Map<String, Object> schedule) {
        List<Object> details = new ArrayList<>();
        for (String[] p : pairs) details.add(automationDetail(p, options, integrity, isDir));
        Map<String, Object> body = obj("name", name, "flowName", name, "transferType", "normal",
                "timezone", env("SCHEDULE_TZ", "Asia/Seoul"), "details", details, "schedules", arr(schedule),
                "step", 1, "isUpcoming", false);
        return body;
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
        String name = env("NAME", "send-one-recurring");
        Object[] fo = fileOptions();
        @SuppressWarnings("unchecked") Map<String, Object> options = (Map<String, Object>) fo[0];
        boolean integrity = (Boolean) fo[1];

        Api api = new Api();
        if (Boolean.TRUE.equals(options.get(SAVEPATH_DEVICE_FLAG))) loadDeviceNames(api);
        System.out.println("[send-one-recurring]  " + pairs.size() + " device pair(s)");

        try {
            Map<String, Object> body = automationBody(name, pairs, options, integrity, isDir, buildSchedule());

            String automationId = asStr(asMap(api.call("POST", "/api/automations", body, null)).get("automationId"));
            System.out.println("automation created: " + automationId);
            @SuppressWarnings("unchecked") Map<String, Object> s = new LinkedHashMap<>((Map<String, Object>) ((List<Object>) body.get("schedules")).get(0));
            s.remove("startDate"); s.remove("startDateType");
            System.out.println("  repeat: every " + env("REPEAT_EVERY", "day") + " " + Json.write(s));
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
