// =============================================================================
//  INNORIX Transfer Builder example — Send Matching Files to Many Devices on a Recurring Schedule
// -----------------------------------------------------------------------------
//  Result page : https://innorix.com/en/builder/send-matching-many-recurring
//  Devices     : one source device -> several target devices (TARGET_IDS)
//  Starts      : on a recurring schedule (REPEAT_EVERY hour | day | week | month)
//  Files       : only files that match FILTER_EXT / FILTER_MIN_MB / FILTER_MAX_MB
//
//  Every setting lives in the .env file next to this script (see .env.builder.example).
//  Requirements:  Node.js 18+ (uses the built-in fetch, no external dependencies)
//  Run:
//    node combo_builder.js                 # use the .env in this folder
//    node combo_builder.js path/to/x.env   # or point it at another .env
// =============================================================================

'use strict';
const fs = require('fs');
const path = require('path');

// ================================================================== .env
function loadEnvFile(file) {
  if (!fs.existsSync(file)) {
    console.log(`note: ${file} not found, using the current environment only`);
    return;
  }
  for (const raw of fs.readFileSync(file, 'utf8').split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith('#') || !line.includes('=')) continue;
    const idx = line.indexOf('=');
    const key = line.slice(0, idx).trim();
    let value = line.slice(idx + 1).trim();
    if (value[0] !== '"' && value[0] !== "'") {
      if (value.startsWith('#')) value = '';
      else if (value.includes(' #')) value = value.split(' #')[0].trimEnd();
    } else if (value.length >= 2 && value[0] === value[value.length - 1]) {
      value = value.slice(1, -1);
    }
    if (process.env[key] === undefined) process.env[key] = value;
  }
}

function env(key, dflt = null) {
  const v = process.env[key];
  return v === undefined || v.trim() === '' ? dflt : v.trim();
}
function envBool(key, dflt = false) {
  const v = env(key);
  return v === null ? dflt : ['1', 'true', 'yes', 'on'].includes(v.toLowerCase());
}
function envInt(key, dflt = null) {
  const v = env(key);
  if (v === null) return dflt;
  if (!/^-?\d+$/.test(v)) fail(`${key} must be an integer, got: ${v}`);
  return parseInt(v, 10);
}
function envFloat(key, dflt = null) {
  const v = env(key);
  if (v === null) return dflt;
  const n = Number(v);
  if (Number.isNaN(n)) fail(`${key} must be a number, got: ${v}`);
  return n;
}
function envList(key) {
  return (env(key) || '').split(',').map((s) => s.trim()).filter(Boolean);
}
function require_(key) {
  const v = env(key);
  if (v === null) fail(`missing required setting in .env: ${key}`);
  return v;
}
function fail(msg) { console.error(msg); process.exit(1); }

// ================================================================== API client
class ApiError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}

class Api {
  constructor() {
    this.base = (env('INNORIX_BASE_URL', 'https://app.innorix.com')).replace(/\/+$/, '');
    this.timeout = (envInt('INNORIX_TIMEOUT', 30)) * 1000;
    this.workspace = env('INNORIX_WORKSPACE_ID');
    // Long-lived user API key -> x-api-key; or a short-lived JWT -> Authorization: Bearer.
    this.apiKey = env('INNORIX_API_KEY');
    this.access = env('INNORIX_ACCESS_TOKEN');
    this.refresh = null;
    this.debug = envBool('DEBUG');   // DEBUG=true -> print every request + response
  }

  async init() {
    if (!this.apiKey && !this.access) {
      const email = env('INNORIX_EMAIL'), password = env('INNORIX_PASSWORD');
      if (!(email && password)) {
        fail('set INNORIX_API_KEY or INNORIX_ACCESS_TOKEN, or INNORIX_EMAIL and INNORIX_PASSWORD in .env');
      }
      await this.login(email, password);
    }
    return this;
  }

  async _fetch(method, url, headers, body) {
    const ctrl = new AbortController();
    const t = setTimeout(() => ctrl.abort(), this.timeout);
    try {
      return await fetch(url, {
        method, headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: ctrl.signal,
      });
    } catch (e) {
      throw new ApiError(0, `cannot reach ${this.base}: ${e.name}`);
    } finally { clearTimeout(t); }
  }

  async login(email, password) {
    const r = await this._fetch('POST', `${this.base}/api/auth/login`,
      { 'Content-Type': 'application/json' }, { email, password });
    if (!r.ok) fail(`login failed (${r.status}): ${(await r.text()).slice(0, 200)}`);
    const user = (await r.json()).data.user;   // tokens are nested under data.user
    this.access = user.accessToken; this.refresh = user.refreshToken;
  }

  async renew() {
    if (!this.refresh) return false;
    const r = await this._fetch('POST', `${this.base}/api/auth/token/refresh`,
      { 'X-Refresh-Token': this.refresh });
    if (!r.ok) return false;
    const data = (await r.json()).data;
    this.access = data.accessToken; this.refresh = data.refreshToken || this.refresh;
    return true;
  }

  async call(method, apiPath, body, params, retried = false) {
    const headers = { 'Content-Type': 'application/json' };
    if (this.apiKey) headers['x-api-key'] = this.apiKey;
    else headers['Authorization'] = `Bearer ${this.access}`;
    if (this.workspace) headers['x-workspace-id'] = this.workspace;
    let url = this.base + apiPath;
    if (params) url += '?' + new URLSearchParams(params).toString();
    if (this.debug) {
      const safe = Object.fromEntries(Object.entries(headers).map(
        ([k, v]) => [k, (k === 'x-api-key' || k === 'Authorization') ? '***' : v]));
      console.log(`\n>> ${method} ${url}`);
      console.log(`   headers: ${JSON.stringify(safe)}`);
      if (body !== undefined) console.log('   body   : ' + JSON.stringify(body, null, 2));
    }
    const r = await this._fetch(method, url, headers, body);
    if (r.status === 401 && !retried && await this.renew()) {
      return this.call(method, apiPath, body, params, true);
    }
    // Read the body as text once, then parse — so DEBUG can print the raw text
    // and errors can surface the full response, not just the status.
    const text = await r.text();
    if (this.debug) console.log(`<< ${r.status} ${r.statusText}\n   response: ${text || '(empty)'}`);
    let payload = {};
    try { payload = text ? JSON.parse(text) : {}; } catch (e) { /* non-JSON */ }
    if (!r.ok) {
      let detail = payload.message || payload.error || text.slice(0, 500) || String(r.status);
      if (text) detail += `  |  full response: ${text}`;
      throw new ApiError(r.status, detail);
    }
    return payload.data;
  }
}

// Automation details use plain absolute paths (forward slashes) in
// sourceItem[].filePath and targetPath — not the {deviceId}_ino_{base64} token.
function plainPath(rawPath) {
  return String(rawPath || '').replace(/\\/g, '/');
}
function nowIso() {
  return new Date().toISOString().replace(/\.\d+Z$/, '.000Z');
}

// ================================================================== devices
// Line a *_PATHS list up with its *_IDS list: 1 path = same for all, N = one each.
function expandPaths(paths, count, pathsKey, idsKey) {
  if (!paths.length) fail(`${pathsKey} is required (one path, or one per ${idsKey})`);
  if (paths.length === 1) return Array(count).fill(paths[0]);
  if (paths.length === count) return paths;
  fail(`${pathsKey} must have 1 entry (same for all) or exactly ${count} (one per ${idsKey})`);
}

// One source (SOURCE_ID + SOURCE_PATH) -> every device in TARGET_IDS (+ TARGET_PATHS).
function devicePairs() {
  const source = require_('SOURCE_ID'), sourcePath = require_('SOURCE_PATH');
  const targets = envList('TARGET_IDS');
  if (!targets.length) fail('missing required setting in .env: TARGET_IDS');
  const paths = expandPaths(envList('TARGET_PATHS'), targets.length, 'TARGET_PATHS', 'TARGET_IDS');
  return targets.map((t, i) => [source, sourcePath, t, paths[i]]);
}

// ================================================================== file options
const DUPLICATE_MAP = { skip: 'nosend', overwrite: 'overwrite', rename: 'numbering' };

// SAVE_PATH=device marks the options object here; automationDetail() swaps it for the
// per-pair source device name (the marker never reaches the request body).
const SAVEPATH_DEVICE_FLAG = '__savepathDevice';
const DEVICE_NAMES = {};

function runDateFolder() {
  // default yymmdd; SAVE_PATH_DATE_FORMAT accepts the yyyy/yy/MM/dd tokens
  const d = new Date(), p = (n) => String(n).padStart(2, '0');
  return env('SAVE_PATH_DATE_FORMAT', 'yymmdd')
    .replace('yyyy', String(d.getFullYear()))
    .replace('yy', String(d.getFullYear()).slice(-2))
    .replace('mm', p(d.getMonth() + 1))
    .replace('MM', p(d.getMonth() + 1))
    .replace('dd', p(d.getDate()));
}

async function loadDeviceNames(api) {
  // deviceId -> device name (GET /api/devices, paginated): savepath must carry the name
  for (let page = 1; page <= 20; page += 1) {
    const result = (await api.call('GET', '/api/devices', undefined, { page, size: 100 })) || {};
    for (const device of result.devices || []) {
      if (device.deviceId) DEVICE_NAMES[device.deviceId] = device.name || device.deviceId;
    }
    if (page >= (result.last_page || result.lastPage || 1)) break;
  }
}

function fileOptions() {
  // Duplicated Name — what to do when the target already has a file with the same name
  const raw = env('DUPLICATE_ACTION', 'overwrite').toLowerCase();
  const action = DUPLICATE_MAP[raw];
  if (!action) fail(`DUPLICATE_ACTION must be overwrite|rename|skip, got: ${raw}`);
  const options = { 'target-action': action };

  // Matching files only — Type Filter (extensions, allow/deny) + Size Filter (MB)
  const fo = {};
  const exts = envList('FILTER_EXT');
  if (exts.length) {
    const allow = env('FILTER_MODE', 'allow').toLowerCase() !== 'deny';
    fo.extension = { extension: exts.map((e) => e.replace(/^\.+/, '').toLowerCase()), allow };
  }
  const minMb = envFloat('FILTER_MIN_MB'), maxMb = envFloat('FILTER_MAX_MB');
  if (minMb !== null) {
    fo.fileSize = { size: Math.round(minMb * 1024 * 1024), over: true, equal: true };
    if (maxMb !== null) console.log(`  WARNING: both size bounds set, but the API's fileSize filter takes ONE bound — only FILTER_MIN_MB (${minMb}MB) is applied and FILTER_MAX_MB (${maxMb}MB) is IGNORED, so larger files will be transferred`);
  } else if (maxMb !== null) {
    fo.fileSize = { size: Math.round(maxMb * 1024 * 1024), over: false, equal: true };
  }
  if (Object.keys(fo).length) options['send-fileoption'] = fo;

  // Save Path: `savepath` carries the TARGET SUBFOLDER NAME as a string; `optionPath` only
  // records which mode produced it (1=run date, 2=device name). The source folder
  // structure is preserved under the target path either way.
  const save = env('SAVE_PATH', 'root').toLowerCase();
  if (['root', 'false'].includes(save)) { /* no subfolder */ }
  else if (save === 'date') { options.savepath = runDateFolder(); options.optionPath = 1; }
  else if (save === 'device') { options.optionPath = 2; options[SAVEPATH_DEVICE_FLAG] = true; }
  else fail(`SAVE_PATH must be root|date|device, got: ${save}`);
  return { options, integrity: envBool('INTEGRITY') };
}

// ================================================================== when it runs
const WEEKDAY = { sun: 'sunday', mon: 'monday', tue: 'tuesday', wed: 'wednesday', thu: 'thursday', fri: 'friday', sat: 'saturday' };
// JS Date.getDay(): Sunday=0 .. Saturday=6
const WEEKDAY_INDEX = { sunday: 0, monday: 1, tuesday: 2, wednesday: 3, thursday: 4, friday: 5, saturday: 6 };

function clock() {
  return { hour: env('REPEAT_HOUR', '12'), minute: env('REPEAT_MINUTE', '00'),
    ampm: env('REPEAT_AMPM', 'am').toLowerCase(), timezone: env('SCHEDULE_TZ', 'Asia/Seoul') };
}
function hour24() {
  return (parseInt(env('REPEAT_HOUR', '12'), 10) % 12) + (env('REPEAT_AMPM', 'am').toLowerCase() === 'pm' ? 12 : 0);
}
function nowInTz(tz) {
  // Wall-clock fields in tz -> a Date built from those fields (used only for its components).
  try {
    const p = new Intl.DateTimeFormat('en-CA', {
      timeZone: tz, year: 'numeric', month: '2-digit', day: '2-digit',
      hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false,
    }).formatToParts(new Date()).reduce((a, x) => (a[x.type] = x.value, a), {});
    const hh = p.hour === '24' ? 0 : parseInt(p.hour, 10);
    return new Date(+p.year, +p.month - 1, +p.day, hh, +p.minute, +p.second);
  } catch (e) {
    console.log(`  note: timezone '${tz}' unavailable; computing first run in local time`);
    return new Date();
  }
}
function fmtNaive(d) {
  const z = (n) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${z(d.getMonth() + 1)}-${z(d.getDate())}T${z(d.getHours())}:${z(d.getMinutes())}:${z(d.getSeconds())}`;
}
function nextOccurrence(every, weekdays, doms) {
  // first future time matching the cadence + clock, in SCHEDULE_TZ
  const now = nowInTz(env('SCHEDULE_TZ', 'Asia/Seoul'));
  if (every === 'hour') {
    const top = new Date(now); top.setMinutes(0, 0, 0);
    if (top <= now) top.setHours(top.getHours() + 1);
    return top;
  }
  const base = new Date(now); base.setHours(hour24(), parseInt(env('REPEAT_MINUTE', '00'), 10), 0, 0);
  for (let i = 0; i < 400; i++) {
    const cand = new Date(base); cand.setDate(base.getDate() + i);
    if (cand <= now) continue;
    if (every === 'day') return cand;
    if (every === 'week' && weekdays.has(cand.getDay())) return cand;
    if (every === 'month') {
      const last = new Date(cand.getFullYear(), cand.getMonth() + 1, 0).getDate();
      if (doms.has(cand.getDate()) || (doms.has(0) && cand.getDate() === last)) return cand;
    }
  }
  return base;
}
function startFields(every, weekdays, doms) {
  // REPEAT_START_AT wins; else REPEAT_RUN_NOW=true also runs once now;
  // default: no immediate run, the first run is the next occurrence.
  const startAt = env('REPEAT_START_AT');
  if (startAt) return { startDateType: 'specific', startDate: startAt };
  if (envBool('REPEAT_RUN_NOW', false)) return { startDateType: 'now', startDate: nowIso() };
  const first = fmtNaive(nextOccurrence(every, weekdays, doms));
  console.log(`  first run at ${first} (${env('SCHEDULE_TZ', 'Asia/Seoul')}); no immediate run`);
  return { startDateType: 'specific', startDate: first };
}

// Recurring: schedule type hour | day | week | month.
function buildSchedule() {
  const every = env('REPEAT_EVERY', 'day').toLowerCase();
  if (!['hour', 'day', 'week', 'month'].includes(every)) fail(`REPEAT_EVERY must be hour|day|week|month, got: ${every}`);
  const weekdays = new Set(), doms = new Set();
  const sched = { type: every, ...clock() };
  if (every === 'week') {
    const names = [];
    for (const d of (envList('REPEAT_WEEKDAY').length ? envList('REPEAT_WEEKDAY') : ['monday'])) {
      const key = d.toLowerCase().slice(0, 3);
      if (!WEEKDAY[key]) fail(`REPEAT_WEEKDAY: invalid weekday '${d}' (sunday..saturday)`);
      names.push(WEEKDAY[key]); weekdays.add(WEEKDAY_INDEX[WEEKDAY[key]]);
    }
    sched.dayInWeek = names;
  } else if (every === 'month') {
    const dates = [];
    for (const d of (envList('REPEAT_DAY_OF_MONTH').length ? envList('REPEAT_DAY_OF_MONTH') : ['1'])) {
      const day = ['lastday', 'last'].includes(d.trim().toLowerCase()) ? '0' : d;   // "Last day" -> 0
      if (!/^\d+$/.test(day) || +day < 0 || +day > 31) fail(`REPEAT_DAY_OF_MONTH: invalid day '${d}' (1-31, or 'last' = last day)`);
      dates.push(String(+day)); doms.add(+day);
    }
    sched.dayInMonth = dates;
  }
  Object.assign(sched, startFields(every, weekdays, doms));
  return sched;
}

// ================================================================== automation body
function automationDetail(pair, options, integrity, isDir) {
  const [source, sourcePath, target, targetPath] = pair;
  const detailOptions = { ...options };
  if (detailOptions[SAVEPATH_DEVICE_FLAG]) {
    delete detailOptions[SAVEPATH_DEVICE_FLAG];
    // subfolder per SOURCE device
    detailOptions.savepath = DEVICE_NAMES[source] || source;
  }
  if (integrity) detailOptions.checkIntegrity = true;
  // Plain absolute paths: sourceItem[].filePath for the source, targetPath for the
  // destination (both forward-slashed). targetPath must not be "/" or empty.
  const item = { filePath: plainPath(sourcePath), isDir };
  return {
    senderId: source, receiverId: target, sourceItem: [item],
    targetPath: plainPath(targetPath), step: 1,
    transferOptions: { noSchedule: false, ...detailOptions },
  };
}

function automationBody(name, pairs, options, integrity, isDir, schedule) {
  const body = {
    name, flowName: name, transferType: 'normal', timezone: env('SCHEDULE_TZ', 'Asia/Seoul'),
    details: pairs.map((p) => automationDetail(p, options, integrity, isDir)),
    schedules: [schedule],
    step: 1, isUpcoming: false,
  };
  return body;
}

// ================================================================== main
async function main() {
  loadEnvFile(process.argv[2] || path.join(__dirname, '.env'));

  const pairs = devicePairs();
  const isDir = envBool('SOURCE_IS_DIR', true);
  const name = env('NAME') || 'send-matching-many-recurring';
  const { options, integrity } = fileOptions();

  const api = await new Api().init();
  if (options[SAVEPATH_DEVICE_FLAG]) await loadDeviceNames(api);
  console.log(`[send-matching-many-recurring]  ${pairs.length} device pair(s)`);

  try {
    const body = automationBody(name, pairs, options, integrity, isDir, buildSchedule());

    const automationId = (await api.call('POST', '/api/automations', body)).automationId;
    console.log(`automation created: ${automationId}`);
    const s = { ...body.schedules[0] }; delete s.startDate; delete s.startDateType;
    console.log(`  repeat: every ${env('REPEAT_EVERY', 'day')} ${JSON.stringify(s)}`);
  } catch (e) {
    if (e instanceof ApiError) fail(`API error ${e.status}: ${e.message}`);
    fail(String(e.message || e));
  }
}

main();
