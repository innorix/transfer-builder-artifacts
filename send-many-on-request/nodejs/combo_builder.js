// =============================================================================
//  INNORIX Transfer Builder example — Send Files to Many Devices When an External Request Arrives
// -----------------------------------------------------------------------------
//  Result page : https://innorix.com/en/builder/send-many-on-request
//  Devices     : one source device -> several target devices (TARGET_IDS)
//  Starts      : when an external system calls the trigger URL printed by the script
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
// On external request: transferType=command, started only by a call to the trigger URL.
// startDateType must NOT be 'now' here — that would also run the automation once at creation.
function buildSchedule() {
  return { type: 'none', startDateType: 'specific', startDate: nowIso(), timezone: env('SCHEDULE_TZ', 'Asia/Seoul') };
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
    name, flowName: name, transferType: 'command', timezone: env('SCHEDULE_TZ', 'Asia/Seoul'),
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
  const name = env('NAME') || 'send-many-on-request';
  const { options, integrity } = fileOptions();

  const api = await new Api().init();
  if (options[SAVEPATH_DEVICE_FLAG]) await loadDeviceNames(api);
  console.log(`[send-many-on-request]  ${pairs.length} device pair(s)`);

  try {
    const body = automationBody(name, pairs, options, integrity, isDir, buildSchedule());

    // the trigger code + key an external system uses to start this automation
    const code = ((await api.call('POST', '/api/command/generate-code')) || {}).code;
    const apiKey = ((await api.call('GET', '/api/command/generate-api-key')) || {}).apiKey;
    if (!code || !apiKey) fail('failed to issue command code / api key');
    body.code = code; body.apiKey = apiKey;

    const automationId = (await api.call('POST', '/api/automations', body)).automationId;
    console.log(`automation created: ${automationId}`);
    const triggerBase = (env('INNORIX_TRIGGER_BASE_URL', api.base)).replace(/\/+$/, '');
    const url = `${triggerBase}/command/${body.code}`;
    console.log(`  endpoint URL : ${url}`);
    console.log(`  x-api-key    : ${body.apiKey}   (secret — anyone with it can start this transfer)`);
    if (envBool('FIRE_TRIGGER')) {
      const r = await api._fetch('POST', url, { 'x-api-key': body.apiKey });
      console.log(`  trigger fired: ${r.status} ${(await r.text()).slice(0, 120)}`);
    }
  } catch (e) {
    if (e instanceof ApiError) fail(`API error ${e.status}: ${e.message}`);
    fail(String(e.message || e));
  }
}

main();
