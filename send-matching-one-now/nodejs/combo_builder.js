// =============================================================================
//  INNORIX Transfer Builder example (combo_builder.js)  —  Node.js port
// -----------------------------------------------------------------------------
//  Runs the configuration you picked in the INNORIX "Transfer Builder" from a
//  single .env file. Behaves the same as the Python version (combo_builder.py).
//  See the README (README.md) for details.
//
//  Requirements:  Node.js 18+ (uses the built-in fetch, no external dependencies)
//  Run:
//    node combo_builder.js                 # use the .env in this folder
//    node combo_builder.js path/to/x.env   # or point it at another .env
//
//  Two axes: TRANSFER_TYPE(send_one/send_many/collect/sync) x START_WHEN(now/schedule/
//  repeat/after_previous/external). Authenticate with INNORIX_API_KEY (x-api-key), or
//  INNORIX_ACCESS_TOKEN (Bearer), or INNORIX_EMAIL + INNORIX_PASSWORD login.
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
function envJson(key, dflt = null) {
  const v = env(key);
  if (v === null) return dflt;
  try { return JSON.parse(v); } catch (e) { fail(`${key} must be valid JSON: ${e.message}`); }
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

const TERMINAL = new Set([2, 4, 5, 9, 99]);   // complete, error, cancelled, partial, fail
const STATUS = {
  '-1': 'queued', 0: 'waiting', 1: 'started', 2: 'complete', 3: 'paused', 4: 'error',
  5: 'cancelled', 6: 'transferring', 7: 'skipped', 8: 'retry', 9: 'partial-complete',
  10: 'retry-checking', 11: 'virus-scanning', 12: 'syncing', 13: 'incoming', 14: 'activity', 99: 'fail',
};

const sleep = (ms) => new Promise((res) => setTimeout(res, ms));

async function waitFor(api, monitorId, timeoutSec, intervalSec = 3, heartbeatSec = 15) {
  // Poll GET /api/transfers/{monitorId} until terminal; print on change + heartbeat.
  const start = Date.now();
  let lastBeat = start, lastChange = start, lastState = null;
  while ((Date.now() - start) / 1000 < timeoutSec) {
    const detail = (await api.call('GET', `/api/transfers/${monitorId}`)) || {};
    const status = detail.status;
    const percent = detail.percent ?? detail.progress ?? 0;
    const now = Date.now();
    const state = `${status}|${percent}`;
    if (state !== lastState) { lastState = state; lastChange = now; }
    if (state !== lastState || now - lastBeat >= heartbeatSec * 1000 || lastBeat === start) {
      const stalled = Math.floor((now - lastChange) / 1000);
      const note = stalled >= 60 ? `  — no progress for ${stalled}s; check the target agent/connection` : '';
      console.log(`    status: ${STATUS[status] ?? status} (${percent}%)  [${Math.floor((now - start) / 1000)}s elapsed]${note}`);
      lastBeat = now;
    }
    if (detail.isTerminal ?? TERMINAL.has(status)) return detail;
    await sleep(intervalSec * 1000);
  }
  throw new Error(`transfer ${monitorId} did not finish within ${timeoutSec}s ` +
    '(still running — raise WAIT_TIMEOUT, or set WAIT=false to not wait)');
}

// Automation details use plain absolute paths (forward slashes) in
// sourceItem[].filePath and targetPath — not the {deviceId}_ino_{base64} token.
function plainPath(rawPath) {
  return String(rawPath || '').replace(/\\/g, '/');
}
function nowIso() {
  return new Date().toISOString().replace(/\.\d+Z$/, '.000Z');
}

// ================================================================== devices (axis 1)
// Line a *_PATHS list up with its *_IDS list: 1 path = same for all, N = one each.
function expandPaths(paths, count, pathsKey, idsKey) {
  if (!paths.length) fail(`${pathsKey} is required for this transfer type (one path, or one per ${idsKey})`);
  if (paths.length === 1) return Array(count).fill(paths[0]);
  if (paths.length === count) return paths;
  fail(`${pathsKey} must have 1 entry (same for all) or exactly ${count} (one per ${idsKey})`);
}

// Resolve the device list + its paths, with a fallback to the single keys: if the *_IDS
// list is empty use the single *_ID, and if the *_PATHS list is empty use the single *_PATH.
function resolveList(listIdsKey, singleIdKey, listPathsKey, singlePathKey, kind) {
  let ids = envList(listIdsKey);
  if (!ids.length) {
    const one = env(singleIdKey);
    if (!one) fail(`${kind} needs ${listIdsKey} (or a single ${singleIdKey})`);
    ids = [one];
  }
  let paths = envList(listPathsKey);
  if (!paths.length && env(singlePathKey)) paths = [env(singlePathKey)];
  paths = expandPaths(paths, ids.length, `${listPathsKey} (or ${singlePathKey})`, listIdsKey);
  return [ids, paths];
}

function devicePairs(transferType) {
  // send_many : one source (SOURCE_ID + SOURCE_PATH) + target list (TARGET_IDS + TARGET_PATHS)
  // collect   : one target (TARGET_ID + TARGET_PATH) + source list (SOURCE_IDS + SOURCE_PATHS)
  // send_one/sync : single SOURCE_* and TARGET_* pair
  if (transferType === 'send_many') {
    const source = require_('SOURCE_ID'), sourcePath = require_('SOURCE_PATH');
    const [targets, paths] = resolveList('TARGET_IDS', 'TARGET_ID', 'TARGET_PATHS', 'TARGET_PATH', 'send_many');
    return targets.map((t, i) => [source, sourcePath, t, paths[i]]);
  }
  if (transferType === 'collect') {
    const target = require_('TARGET_ID'), targetPath = require_('TARGET_PATH');
    const [sources, paths] = resolveList('SOURCE_IDS', 'SOURCE_ID', 'SOURCE_PATHS', 'SOURCE_PATH', 'collect');
    return sources.map((s, i) => [s, paths[i], target, targetPath]);
  }
  const source = require_('SOURCE_ID'), sourcePath = require_('SOURCE_PATH');   // send_one, sync
  const target = require_('TARGET_ID'), targetPath = require_('TARGET_PATH');
  return [[source, sourcePath, target, targetPath]];
}

// ================================================================== file options
const DUPLICATE_MAP = { skip: 'nosend', overwrite: 'overwrite', rename: 'numbering', nosend: 'nosend', numbering: 'numbering' };

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
  const raw = env('DUPLICATE_ACTION', 'overwrite').toLowerCase();
  const action = DUPLICATE_MAP[raw];
  if (!action) fail(`DUPLICATE_ACTION must be overwrite|rename|skip, got: ${raw}`);
  const options = { 'target-action': action };

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
  const exclude = env('FILTER_EXCLUDE');
  if (exclude) fo.fileName = { name: exclude, allow: false };
  if (Object.keys(fo).length) options['send-fileoption'] = fo;

  // Save Path: `savepath` carries the TARGET SUBFOLDER NAME as a string; `optionPath` only
  // records which mode produced it (1=run date, 2=device name, 3=custom). The server does not
  // build the name from optionPath, so sending a boolean creates a folder named "true".
  // The source folder structure is preserved under the target path either way.
  const save = env('SAVE_PATH', 'false').toLowerCase();
  if (['false', 'off', 'no', '0', 'true', 'on', 'yes', '1', 'root'].includes(save)) { /* no subfolder */ }
  else if (['date', 'yymmdd', 'start_date'].includes(save)) { options.savepath = runDateFolder(); options.optionPath = 1; }
  else if (['device', 'device_name'].includes(save)) { options.optionPath = 2; options[SAVEPATH_DEVICE_FLAG] = true; }
  else if (['custom', 'custom_name'].includes(save)) { options.savepath = require_('SAVE_PATH_NAME'); options.optionPath = 3; }
  else if (['transfer-id', 'transfer_id', 'transferid'].includes(save)) fail('SAVE_PATH=transfer-id is not supported: the target subfolder name must be known before the transfer is created, so the transfer ID cannot be used. Use date, device or custom.');
  else fail(`SAVE_PATH must be root|date|device|custom (or true/false), got: ${save}`);
  return { options, integrity: envBool('INTEGRITY') };
}

// ================================================================== after transfer
const INTEGRATIONS = [   // toggle, category, .env prefix (Monitoring is a processor, see below)
  ['ON_MESSAGE', 'notification', 'MESSAGE'],
  ['ON_VIRUS_SCAN', 'security', 'VIRUS_SCAN'],
  ['ON_EMAIL', 'email', 'EMAIL'],
];

function notifyEvents(prefix) {
  // <PREFIX>_EVENTS (per action, from the builder's Timing) wins over the shared NOTIFY_EVENTS
  const perAction = prefix ? envList(`${prefix}_EVENTS`) : [];
  const evs = perAction.length ? perAction : envList('NOTIFY_EVENTS').length ? envList('NOTIFY_EVENTS') : ['completed', 'error'];
  return Object.fromEntries(evs.map((e) => [e, true]));
}

async function ensureIntegrations(api) {
  for (const [toggle, category, prefix] of INTEGRATIONS) {
    if (!envBool(toggle)) continue;
    const events = notifyEvents(prefix);
    const provider = require_(`${prefix}_TYPE`).toLowerCase();
    const config = envJson(`${prefix}_CONFIG`, {}) || {};
    if (typeof config !== 'object' || Array.isArray(config)) fail(`${prefix}_CONFIG must be a JSON object`);
    const name = env(`${prefix}_NAME`) || config.name || `builder-${provider}`;
    if (config.name === undefined) config.name = name;

    const rules = (await api.call('GET', `/api/integrations/rules/${provider}`)) || {};
    const rule = rules[provider] || Object.values(rules)[0] || null;
    if (!rule) console.log(`  ! ${toggle}: unknown provider '${provider}' — check ${prefix}_TYPE`);
    else {
      const missing = (rule.fields || []).filter((f) => f.required && config[f.id] === undefined).map((f) => f.id);
      if (missing.length) {
        const detail = `${toggle}: ${prefix}_CONFIG is missing required fields: ${missing.join(', ')} — the integration is saved but will not work`;
        if (envBool('INTEGRATION_STRICT', false)) fail(`  ! ${detail} (INTEGRATION_STRICT=true)`);
        console.log(`  ! ${detail}`);
      }
    }

    const existing = (await api.call('GET', '/api/integrations', undefined, { category })) || [];
    const found = existing.find((it) => it.type === provider && it.name === name);
    if (found) { console.log(`  ${toggle}: ${category}/${provider} '${name}' already exists, reused`); continue; }
    const bodyReq = { name, type: provider, category, config, notificationConfig: { events } };
    for (const k of ['webhookUrl', 'baseUrl', 'mode', 'authType']) if (config[k] !== undefined) bodyReq[k] = config[k];
    if (config.webhookUrl !== undefined && bodyReq.mode === undefined) bodyReq.mode = 'webhook';
    const result = (await api.call('POST', '/api/integrations', bodyReq)) || {};
    console.log(`  ${toggle}: ${category}/${provider} '${name}' created (${result.integrationId || result.id})`);
  }
}

function runApiProcessor() {
  // Public API spec: a run action needs category="run" and type="http"|"command"|"script",
  // and every parameter goes under `config` (url / method / body / deviceId / events /
  // waitForResult). Top-level url/method are deprecated, and a processor without
  // category="run" is dropped by the server without an error.
  if (!envBool('ON_RUN_API')) return null;
  let action = env('RUN_API_TYPE', 'http').toLowerCase();
  if (action === 'https') action = 'http';            // builder used to emit this
  if (!['http', 'command', 'script'].includes(action)) fail(`RUN_API_TYPE must be http|command|script, got: ${action}`);

  const config = {};
  if (action === 'http') {
    config.url = require_('RUN_API_URL');
    config.method = env('RUN_API_METHOD', 'POST').toUpperCase();
    if (env('RUN_API_BODY')) {
      const parsed = envJson('RUN_API_BODY', null);
      if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) fail('RUN_API_BODY must be a JSON object, e.g. {"key":"value"}');
      config.body = parsed;
    }
  } else if (action === 'command') {
    config.command = require_('RUN_API_COMMAND');
  } else {
    config.file = require_('RUN_API_SCRIPT');
    if (env('RUN_API_ARGS')) config.args = env('RUN_API_ARGS');
  }

  // Run On: source device = senderId, target device = receiverId (omit -> the transfer's device)
  const device = env('RUN_API_DEVICE', '').toLowerCase();
  if (['source', 'sender'].includes(device)) config.deviceId = require_('SOURCE_ID');
  else if (['target', 'receiver'].includes(device)) config.deviceId = require_('TARGET_ID');
  else if (device) config.deviceId = env('RUN_API_DEVICE');

  // config.events = {completed: true, ...}; omit to fire on every event.
  // "Run" is a builder label, not a transfer event, so it is ignored here.
  const events = envList('RUN_API_EVENTS').filter((e) => e.toLowerCase() !== 'run');
  if (events.length) config.events = Object.fromEntries(events.map((e) => [e, true]));
  if (envBool('RUN_API_WAIT', false)) config.waitForResult = true;

  return { category: 'run', type: action, name: env('RUN_API_NAME', 'builder-run-api'), config };
}

function monitoringProcessor() {
  if (!envBool('ON_MONITORING')) return null;
  const provider = require_('MONITORING_TYPE').toLowerCase();
  const config = envJson('MONITORING_CONFIG', {}) || {};
  if (typeof config !== 'object' || Array.isArray(config)) fail('MONITORING_CONFIG must be a JSON object');
  const name = env('MONITORING_NAME') || config.name || `builder-${provider}`;
  if (config.name === undefined) config.name = name;
  return { category: 'monitoring', type: provider, name, config, notificationConfig: { events: notifyEvents('MONITORING') } };
}

function afterTransferProcessors() {
  return [runApiProcessor(), monitoringProcessor()].filter(Boolean);
}

// ================================================================== schedule (axis 2)
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
  const startAt = env('REPEAT_START_AT');
  if (startAt) return { startDateType: 'specific', startDate: startAt };
  if (envBool('REPEAT_RUN_NOW', false)) return { startDateType: 'now', startDate: nowIso() };
  const first = fmtNaive(nextOccurrence(every, weekdays, doms));
  console.log(`  first run at ${first} (${env('SCHEDULE_TZ', 'Asia/Seoul')}); no immediate run`);
  return { startDateType: 'specific', startDate: first };
}
function buildRepeatSchedule() {
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
      const day = ['lastday', 'last', 'last_day', 'eom'].includes(d.trim().toLowerCase()) ? '0' : d;   // builder's "Last day"
      if (!/^\d+$/.test(day) || +day < 0 || +day > 31) fail(`REPEAT_DAY_OF_MONTH: invalid day '${d}' (1-31, 0 or 'lastday' = last day)`);
      dates.push(String(+day)); doms.add(+day);
    }
    sched.dayInMonth = dates;
  }
  Object.assign(sched, startFields(every, weekdays, doms));
  return sched;
}
// hour/minute/ampm for a 'YYYY-MM-DDTHH:MM' stamp (12-hour clock, as the API wants)
function clockFrom(stamp) {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(String(stamp || ''));
  if (!m) return {};
  const h = Number(m[4]);
  return { hour: String(h % 12 || 12).padStart(2, '0'), minute: m[5], ampm: h >= 12 ? 'pm' : 'am' };
}

function buildSchedule(startWhen) {
  const tz = env('SCHEDULE_TZ', 'Asia/Seoul');
  if (startWhen === 'schedule') {
    // the clock fields must travel with startDate: without them the server falls back to its
    // own default clock (12 pm UTC) and runs at the wrong hour — and on the wrong day
    const at = require_('SCHEDULE_AT');
    return { type: 'none', startDateType: 'specific', startDate: at, timezone: tz, ...clockFrom(at) };
  }
  if (startWhen === 'repeat') return buildRepeatSchedule();
  if (startWhen === 'after_previous') return { type: 'none', startDateType: 'now', startDate: nowIso(), timezone: tz, triggerAutomation: { value: require_('PREV_AUTOMATION_ID') } };
  // startDateType must NOT be 'now' for a command trigger — that runs the automation once at creation
  if (startWhen === 'external') return { type: 'none', startDateType: 'specific', startDate: nowIso(), timezone: tz };
  return null;
}

// ================================================================== automation body
function automationDetail(pair, options, integrity, isDir, isSync) {
  const [source, sourcePath, target, targetPath] = pair;
  const detailOptions = { ...options };
  if (detailOptions[SAVEPATH_DEVICE_FLAG]) {
    delete detailOptions[SAVEPATH_DEVICE_FLAG];
    // subfolder per SOURCE device, so a collect (N:1) keeps each sender separate
    detailOptions.savepath = DEVICE_NAMES[source] || source;
  }
  if (integrity) detailOptions.checkIntegrity = true;
  if (isSync) {
    detailOptions.syncType = env('SYNC_DIRECTION', 'one_way').toLowerCase() === 'two_way' ? 2 : 1;
    const watch = env('SYNC_WATCH', 'file_created').toLowerCase();
    const watchMap = { file_created: 1, file_modified: 2, both: 3 };   // both needs server support
    if (!(watch in watchMap)) fail(`SYNC_WATCH must be file_created|file_modified|both, got: ${watch}`);
    detailOptions.watchFolderType = watchMap[watch];
  }
  // Plain absolute paths: sourceItem[].filePath for the source, targetPath for the
  // destination (both forward-slashed). targetPath must not be "/" or empty.
  const item = { filePath: plainPath(sourcePath), isDir };
  return {
    senderId: source, receiverId: target, sourceItem: [item],   // aliases sourceId/targetId also accepted
    targetPath: plainPath(targetPath), step: 1,
    transferOptions: { noSchedule: false, ...detailOptions },
  };
}

function automationBody(name, pairs, options, integrity, isDir, schedule, processors, isSync) {
  const startWhen = env('START_WHEN');
  const transferType = isSync ? 'sync' : (startWhen === 'external' ? 'command' : 'normal');
  const tz = env('SCHEDULE_TZ', 'Asia/Seoul');
  const body = {
    name, flowName: name, transferType, timezone: tz,
    details: pairs.map((p) => automationDetail(p, options, integrity, isDir, isSync)),
    schedules: [schedule || { type: 'none', startDateType: 'now', startDate: nowIso(), timezone: tz }],
    step: 1, isUpcoming: false,
  };
  let flowId = env('FLOW_ID');
  if (startWhen === 'after_previous' && !flowId) flowId = `builder-flow-${Math.floor(Date.now() / 1000)}`;
  if (flowId) body.flowId = flowId;
  if (processors.length) body.processors = processors;
  return body;
}

// ================================================================== immediate transfer
async function waitAll(api, monitorIds) {
  let failed = 0;
  for (const monitorId of monitorIds) {
    const detail = await waitFor(api, monitorId, envInt('WAIT_TIMEOUT', 3600));
    const status = detail.status;
    console.log(`  finished ${monitorId}: ${STATUS[status] ?? status} (${detail.fileCount || 0} files)`);
    if (status !== 2) failed++;
  }
  return failed ? 1 : 0;
}

// Non-transfer summary row kinds in the active list — skip these; a real transfer
// row has type "monitor" (its top-level monitorId is the id to poll).
const SKIP_ROW_TYPES = new Set(['automation', 'history', 'flow']);

async function activeMonitorIds(api, automationId) {
  // monitorIds of this automation's currently-active transfers, from the active-transfer
  // list GET /api/transfers?automationId=... . This shows a transfer as soon as it STARTS
  // (unlike the execution log, which reflects finished runs). The response is
  // cursor-paginated: data = { data: [records], pagination: {...} } and a transfer row
  // looks like { type: 'monitor', monitorId: 'T...', status: 1, ... }.
  const result = await api.call('GET', '/api/transfers', undefined, { automationId });
  const records = (result && typeof result === 'object' && !Array.isArray(result)) ? (result.data || []) : (result || []);
  return records
    .filter((r) => !SKIP_ROW_TYPES.has(r.type))   // skip automation/history summary rows
    .map((r) => r.monitorId || r.id)              // monitorId (== id) is what we poll
    .filter(Boolean);
}

async function resolveMonitorIds(api, automationId, expected, intervalSec = 3, appearWaitSec = 120) {
  // Poll the active-transfer list until the run(s) start and collect their monitorIds
  // (accumulated across polls, so a short transfer that finishes between polls is not missed).
  const start = Date.now();
  const seen = [];
  for (;;) {
    for (const mid of await activeMonitorIds(api, automationId)) if (!seen.includes(mid)) seen.push(mid);
    if (seen.length >= expected || (Date.now() - start) / 1000 >= appearWaitSec) {
      return seen.length >= expected ? seen.slice(0, expected) : seen;
    }
    console.log(`    waiting for the run to start... (${seen.length}/${expected}) [${Math.floor((Date.now() - start) / 1000)}s]`);
    await sleep(intervalSec * 1000);
  }
}

async function runImmediateAutomation(api, name, pairs, options, integrity, isDir, processors) {
  // START_WHEN=now -> ONE POST /api/automations with a type=none,
  // startDateType=now schedule (runs immediately). All pairs go in details[].
  const tz = env('SCHEDULE_TZ', 'Asia/Seoul');
  const schedule = { type: 'none', startDateType: 'now', startDate: nowIso(), timezone: tz };
  const body = automationBody(name, pairs, options, integrity, isDir, schedule, processors, false);
  const automationId = (await api.call('POST', '/api/automations', body)).automationId;
  console.log(`  automation created: ${automationId}  (immediate: schedule type=none, startDateType=now)`);

  if (!envBool('WAIT', true)) {
    console.log(`  (WAIT=false) not waiting; find the transfer with GET /api/transfers?automationId=${automationId}`);
    return 0;
  }
  console.log('  waiting for the run to start and complete (polling every 3s; set WAIT=false to skip)...');
  const appearWait = envInt('APPEAR_WAIT', 120);
  const monitorIds = await resolveMonitorIds(api, automationId, pairs.length, 3, appearWait);
  if (!monitorIds.length) {
    console.log(`  ERROR: no transfer started within ${appearWait}s — the automation was created but nothing ran. Check GET /api/transfers?automationId=${automationId}`);
    return 1;
  }
  const missing = pairs.length - monitorIds.length;
  if (missing > 0) {
    console.log(`  ERROR: only ${monitorIds.length} of ${pairs.length} transfer(s) started within ${appearWait}s (raise APPEAR_WAIT if the devices are slow to pick the run up)`);
  }
  console.log(`  run started: ${monitorIds.length} transfer(s) — ${monitorIds.join(', ')}`);
  const failed = await waitAll(api, monitorIds);
  return failed || missing > 0 ? 1 : 0;
}

// ================================================================== main
const TRANSFER_TYPES = ['send_one', 'send_many', 'collect', 'sync'];
const START_WHENS = ['now', 'schedule', 'repeat', 'after_previous', 'external'];

async function main() {
  const envPath = process.argv[2] || path.join(__dirname, '.env');
  loadEnvFile(envPath);

  const transferType = env('TRANSFER_TYPE', 'send_one').toLowerCase();
  if (!TRANSFER_TYPES.includes(transferType)) fail(`TRANSFER_TYPE must be ${TRANSFER_TYPES.join('|')}, got: ${transferType}`);
  const isSync = transferType === 'sync';

  const startWhen = env('START_WHEN', 'now').toLowerCase();
  if (!isSync && !START_WHENS.includes(startWhen)) fail(`START_WHEN must be ${START_WHENS.join('|')}, got: ${startWhen}`);

  const pairs = devicePairs(transferType);
  const isDir = envBool('SOURCE_IS_DIR', true);
  const name = env('NAME') || `builder ${transferType} ${startWhen}`;
  const { options, integrity } = fileOptions();
  const processors = afterTransferProcessors();

  const api = await new Api().init();
  if (options[SAVEPATH_DEVICE_FLAG]) await loadDeviceNames(api);
  console.log(`[${transferType} / ${isSync ? 'sync' : startWhen}]  ${pairs.length} device pair(s)`);

  try {
    if (INTEGRATIONS.some(([t]) => envBool(t))) { console.log('integrations:'); await ensureIntegrations(api); }

    if (!isSync && startWhen === 'now') {
      console.log('transfer:');
      process.exit(await runImmediateAutomation(api, name, pairs, options, integrity, isDir, processors));
    }

    const schedule = isSync ? null : buildSchedule(startWhen);
    const body = automationBody(name, pairs, options, integrity, isDir, schedule, processors, isSync);

    if (!isSync && startWhen === 'external') {
      const code = ((await api.call('POST', '/api/command/generate-code')) || {}).code;
      const apiKey = ((await api.call('GET', '/api/command/generate-api-key')) || {}).apiKey;
      if (!code || !apiKey) fail('failed to issue command code / api key');
      body.code = code; body.apiKey = apiKey;
    }

    const automationId = (await api.call('POST', '/api/automations', body)).automationId;
    console.log(`automation created: ${automationId}`);

    if (isSync) {
      const direction = env('SYNC_DIRECTION', 'one_way').toLowerCase() === 'two_way' ? 'two-way' : 'one-way';
      const watch = { file_created: 'on file create', file_modified: 'on file modify', both: 'on create and modify' }[env('SYNC_WATCH', 'file_created').toLowerCase()];
      console.log(`  sync: ${direction}, ${watch}`);
    } else if (startWhen === 'schedule') {
      console.log(`  schedule: once at ${env('SCHEDULE_AT')}`);
    } else if (startWhen === 'repeat') {
      const s = { ...schedule }; delete s.startDate; delete s.startDateType;
      console.log(`  repeat: every ${env('REPEAT_EVERY', 'day')} ${JSON.stringify(s)}`);
    } else if (startWhen === 'after_previous') {
      console.log(`  chained after automation: ${env('PREV_AUTOMATION_ID')} (flowId ${body.flowId})`);
    } else if (startWhen === 'external') {
      const triggerBase = (env('INNORIX_TRIGGER_BASE_URL', api.base)).replace(/\/+$/, '');
      const url = `${triggerBase}/command/${body.code}`;
      console.log(`  endpoint URL : ${url}`);
      console.log(`  x-api-key    : ${body.apiKey}   (secret — anyone with it can start this transfer)`);
      if (envBool('FIRE_TRIGGER')) {
        const r = await api._fetch('POST', url, { 'x-api-key': body.apiKey });
        console.log(`  trigger fired: ${r.status} ${(await r.text()).slice(0, 120)}`);
      }
    }
  } catch (e) {
    if (e instanceof ApiError) fail(`API error ${e.status}: ${e.message}`);
    fail(String(e.message || e));
  }
}

if (require.main === module) main();

// exported for tests only (does not affect CLI use)
module.exports = { devicePairs, fileOptions, buildRepeatSchedule, buildSchedule, automationBody, automationDetail, plainPath, loadEnvFile };
