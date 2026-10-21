// =============================================================================
//  INNORIX Transfer Builder example — Send Files to Many Devices When an External Request Arrives With Follow-Up Actions
// -----------------------------------------------------------------------------
//  Result page : https://innorix.com/en/builder/send-many-on-request-follow-up
//  Devices     : one source device -> several target devices (TARGET_IDS)
//  Starts      : when an external system calls the trigger URL printed by the script
//  After       : follow-up actions (Run API, Monitoring, Message, Virus Scan, Email)
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

// ================================================================== after transfer (follow-up actions)
// Message / Virus Scan / Email are workspace-wide integrations (POST /api/integrations)
// that fire for the events in NOTIFY_EVENTS. Run API and Monitoring are processors
// attached to this automation (see afterTransferProcessors()).
const INTEGRATIONS = [   // toggle, category, .env prefix
  ['ON_MESSAGE', 'notification', 'MESSAGE'],
  ['ON_VIRUS_SCAN', 'security', 'VIRUS_SCAN'],
  ['ON_EMAIL', 'email', 'EMAIL'],
];

function notifyEvents(prefix) {
  // <PREFIX>_EVENTS (per action) wins over the shared NOTIFY_EVENTS
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

    // warn about missing required fields (the server saves it anyway, but it won't work)
    const rules = (await api.call('GET', `/api/integrations/rules/${provider}`)) || {};
    const rule = rules[provider] || Object.values(rules)[0] || null;
    if (!rule) console.log(`  ! ${toggle}: unknown provider '${provider}' — check ${prefix}_TYPE`);
    else {
      const missing = (rule.fields || []).filter((f) => f.required && config[f.id] === undefined).map((f) => f.id);
      if (missing.length) console.log(`  ! ${toggle}: ${prefix}_CONFIG is missing required fields: ${missing.join(', ')} — the integration is saved but will not work`);
    }

    // reuse an existing integration with the same type + name
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
  // A run action needs category="run" and type="http"|"command"|"script", and every
  // parameter goes under `config` (url / method / body / deviceId / events / waitForResult).
  if (!envBool('ON_RUN_API')) return null;
  let action = env('RUN_API_TYPE', 'http').toLowerCase();
  if (action === 'https') action = 'http';
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

  // Run On: which device runs the action (omit -> the transfer's device)
  const device = env('RUN_API_DEVICE', '').toLowerCase();
  if (device === 'source') config.deviceId = require_('SOURCE_ID');
  else if (device) config.deviceId = env('RUN_API_DEVICE');

  // config.events = {completed: true, ...}; omit to fire on every event.
  const events = envList('RUN_API_EVENTS');
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

function automationBody(name, pairs, options, integrity, isDir, schedule, processors) {
  const body = {
    name, flowName: name, transferType: 'command', timezone: env('SCHEDULE_TZ', 'Asia/Seoul'),
    details: pairs.map((p) => automationDetail(p, options, integrity, isDir)),
    schedules: [schedule],
    step: 1, isUpcoming: false,
  };
  if (processors.length) body.processors = processors;
  return body;
}

// ================================================================== main
async function main() {
  loadEnvFile(process.argv[2] || path.join(__dirname, '.env'));

  const pairs = devicePairs();
  const isDir = envBool('SOURCE_IS_DIR', true);
  const name = env('NAME') || 'send-many-on-request-follow-up';
  const { options, integrity } = fileOptions();
  const processors = afterTransferProcessors();

  const api = await new Api().init();
  if (options[SAVEPATH_DEVICE_FLAG]) await loadDeviceNames(api);
  console.log(`[send-many-on-request-follow-up]  ${pairs.length} device pair(s)`);

  try {
    if (INTEGRATIONS.some(([t]) => envBool(t))) { console.log('integrations:'); await ensureIntegrations(api); }

    const body = automationBody(name, pairs, options, integrity, isDir, buildSchedule(), processors);

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
