import { chromium } from 'playwright-core';
import { promises as fs } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

const EXPECTED_PROTECTED_PATHS = [
  '/api/v1/session',
  '/api/v1/device/identity',
  '/api/v1/device/connectivity',
  '/api/v1/device/capabilities',
  '/api/v1/configuration',
  '/api/v1/services'
];

const ACTION_OUTCOMES = new Set(['accepted', 'failed', 'outcomeUnknown', 'notAttempted']);

class HarnessError extends Error {
  constructor(code, message) {
    super(message);
    this.code = code;
  }
}

function parseArgs(argv) {
  const values = {
    browser: 'chrome',
    supportHubOrigin: 'https://support-hub.integration.test:4443',
    agentOrigin: 'https://rms-pos-agent.localhost:5001',
    startUrl: null,
    output: path.join(process.cwd(), 'pos-browser-evidence.json'),
    timeoutMs: 20000,
    allowDisposableServiceAction: false,
    allowLocalhostDevTest: false,
    serviceId: null
  };

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (argument === '--allow-disposable-service-action') {
      values.allowDisposableServiceAction = true;
      continue;
    }
    if (argument === '--allow-localhost-dev-test') {
      values.allowLocalhostDevTest = true;
      continue;
    }
    if (!argument.startsWith('--')) {
      throw new HarnessError('invalid_argument', `Unexpected argument: ${argument}`);
    }

    const key = argument.slice(2).replace(/-([a-z])/g, (_, letter) => letter.toUpperCase());
    if (!(key in values) || key === 'allowDisposableServiceAction') {
      throw new HarnessError('invalid_argument', `Unsupported argument: ${argument}`);
    }
    const value = argv[index + 1];
    if (!value || value.startsWith('--')) {
      throw new HarnessError('invalid_argument', `Missing value for ${argument}`);
    }
    values[key] = key === 'timeoutMs' ? Number(value) : value;
    index += 1;
  }

  if (!['chrome', 'edge'].includes(values.browser)) {
    throw new HarnessError('invalid_argument', '--browser must be chrome or edge.');
  }
  if (!Number.isInteger(values.timeoutMs) || values.timeoutMs < 5000 || values.timeoutMs > 120000) {
    throw new HarnessError('invalid_argument', '--timeout-ms must be between 5000 and 120000.');
  }
  if (values.allowDisposableServiceAction && !/^(svc-[0-9a-f]{16})$/.test(values.serviceId ?? '')) {
    throw new HarnessError(
      'invalid_argument',
      '--allow-disposable-service-action requires one opaque --service-id value.'
    );
  }
  return values;
}

function exactOrigin(value, label, allowLocalhostDevTest = false) {
  let parsed;
  try {
    parsed = new URL(value);
  } catch {
    throw new HarnessError('invalid_origin', `${label} is not a valid URL.`);
  }
  const isAllowedLocalhostDevOrigin = allowLocalhostDevTest
    && parsed.protocol === 'http:'
    && parsed.hostname.toLowerCase() === 'localhost'
    && parsed.port === '4200';
  if (
    (!isAllowedLocalhostDevOrigin && parsed.protocol !== 'https:')
    || parsed.username
    || parsed.password
    || parsed.search
    || parsed.hash
    || (parsed.pathname !== '' && parsed.pathname !== '/')
    || value.includes('*')
  ) {
    throw new HarnessError('invalid_origin', `${label} must be one exact HTTPS origin.`);
  }
  return parsed.origin.toLowerCase();
}

function safeAgentPath(url, agentOrigin) {
  let parsed;
  try {
    parsed = new URL(url);
  } catch {
    return null;
  }
  if (parsed.origin.toLowerCase() !== agentOrigin) return null;
  if (EXPECTED_PROTECTED_PATHS.includes(parsed.pathname)) return parsed.pathname;
  if (parsed.pathname === '/health/live' || parsed.pathname === '/health/ready') return parsed.pathname;
  if (/^\/api\/v1\/services\/svc-[0-9a-f]{16}\/actions$/.test(parsed.pathname)) return '/api/v1/services/{opaque}/actions';
  if (parsed.pathname === '/api/v1/security/mutation-token') return parsed.pathname;
  return null;
}

function integritySnapshot() {
  const probe = [
    '$identity = [Security.Principal.WindowsIdentity]::GetCurrent()',
    '$principal = New-Object Security.Principal.WindowsPrincipal($identity)',
    '$isAdministrator = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)',
    '$integrity = $identity.Groups | Where-Object { $_.Value -like "S-1-16-*" } | Select-Object -First 1',
    '$rid = if ($null -eq $integrity) { 0 } else { [int]($integrity.Value.Split("-")[-1]) }',
    '$level = switch ($rid) { 4096 { "Low" } 8192 { "Medium" } 8448 { "MediumPlus" } 12288 { "High" } 16384 { "System" } default { "Unknown" } }',
    '[pscustomobject]@{ isAdministrator = $isAdministrator; integrity = $level } | ConvertTo-Json -Compress'
  ].join('; ');
  const result = spawnSync(
    'powershell.exe',
    ['-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-Command', probe],
    { encoding: 'utf8', windowsHide: true, timeout: 10000 }
  );
  if (result.error || result.status !== 0) {
    throw new HarnessError('integrity_probe_failed', 'The temporary browser process integrity could not be verified.');
  }
  try {
    const parsed = JSON.parse(result.stdout.trim());
    let integrity = typeof parsed.integrity === 'string' ? parsed.integrity : 'Unknown';
    if (integrity === 'Unknown') {
      const groups = spawnSync('whoami.exe', ['/groups'], { encoding: 'utf8', windowsHide: true, timeout: 10000 });
      const rid = groups.stdout?.match(/S-1-16-(\d+)/u)?.[1];
      integrity = rid === '4096'
        ? 'Low'
        : rid === '8192'
          ? 'Medium'
          : rid === '8448'
            ? 'MediumPlus'
            : rid === '12288'
              ? 'High'
              : rid === '16384'
                ? 'System'
                : 'Unknown';
    }
    return {
      isAdministrator: parsed.isAdministrator === true,
      integrity
    };
  } catch {
    throw new HarnessError('integrity_probe_failed', 'The temporary browser process integrity could not be verified.');
  }
}

function browserUserAgentMatches(browser, userAgent) {
  if (browser === 'chrome') return /Chrome\/\d+/u.test(userAgent) && !/Edg\/\d+/u.test(userAgent);
  return /Edg\/\d+/u.test(userAgent);
}

function safeError(error) {
  if (error instanceof HarnessError) return { code: error.code, detail: error.message };
  const message = String(error?.message ?? '');
  if (/Executable doesn't exist|browserType\.launch|channel/u.test(message)) {
    return { code: 'browser_unavailable', detail: 'The requested installed browser channel could not be launched.' };
  }
  if (/ERR_CONNECTION_REFUSED|net::ERR_NAME_NOT_RESOLVED|Timeout .* exceeded|Navigation/u.test(message)) {
    return { code: 'support_hub_unavailable', detail: 'The exact Support Hub origin did not return a browser page.' };
  }
  return { code: 'harness_failed', detail: 'The disposable browser evidence run did not complete.' };
}

async function run() {
  const options = parseArgs(process.argv.slice(2));
  const supportHubOrigin = exactOrigin(options.supportHubOrigin, 'SupportHubOrigin', options.allowLocalhostDevTest);
  const agentOrigin = exactOrigin(options.agentOrigin, 'AgentOrigin');
  const startUrl = new URL(options.startUrl ?? `${supportHubOrigin}/tools/pos-maintenance`);
  const isAllowedLocalhostDevOrigin = options.allowLocalhostDevTest && supportHubOrigin === 'http://localhost:4200';
  if (
    startUrl.origin.toLowerCase() !== supportHubOrigin
    || (!isAllowedLocalhostDevOrigin && startUrl.protocol !== 'https:')
  ) {
    throw new HarnessError('origin_mismatch', 'The browser start URL must use the configured exact SupportHubOrigin.');
  }

  const evidence = {
    schemaVersion: 1,
    result: 'blocked',
    generatedUtc: new Date().toISOString(),
    browser: {
      requested: options.browser,
      channel: options.browser === 'chrome' ? 'chrome' : 'msedge',
      headless: false,
      userAgentVerified: false,
      integrity: 'unverified',
      elevated: null
    },
    origins: {
      supportHubOrigin,
      agentOrigin,
      startPath: startUrl.pathname
    },
    checks: {
      page: { status: 'not-run' },
      browserAuthentication: { status: 'not-run', challengeResponses: 0 },
      protectedReads: {},
      authorization: { authenticatedLabel: false, administratorAuthorizedLabel: false },
      serviceControl: { attempted: false, status: 'not-run' },
      disposableProfile: { cookiesBeforeNavigation: 0, cookiesAfterNavigation: 0 }
    },
    error: null
  };

  let context;
  let profileDirectory;
  const responses = new Map();
  let challengeResponses = 0;

  try {
    const integrity = integritySnapshot();
    evidence.browser.integrity = integrity.integrity;
    evidence.browser.elevated = integrity.isAdministrator;
    if (integrity.isAdministrator || !['Medium', 'MediumPlus'].includes(integrity.integrity)) {
      throw new HarnessError('elevated_browser_blocked', 'The browser harness must run as a non-elevated Medium-integrity user.');
    }

    profileDirectory = await fs.mkdtemp(path.join(os.tmpdir(), 'rms-pos-int13c-browser-'));
    context = await chromium.launchPersistentContext(profileDirectory, {
      channel: options.browser === 'chrome' ? 'chrome' : 'msedge',
      headless: false,
      acceptDownloads: false,
      bypassCSP: false,
      ignoreHTTPSErrors: false,
      args: ['--no-first-run', '--no-default-browser-check']
    });
    context.setDefaultTimeout(options.timeoutMs);
    const page = context.pages()[0] ?? await context.newPage();
    const initialCookies = await context.cookies();
    evidence.checks.disposableProfile.cookiesBeforeNavigation = initialCookies.length;
    if (initialCookies.length !== 0) {
      throw new HarnessError('disposable_profile_not_empty', 'The disposable browser profile was not empty.');
    }

    page.on('response', response => {
      const safePath = safeAgentPath(response.url(), agentOrigin);
      if (!safePath) return;
      const current = responses.get(safePath) ?? { status: response.status(), count: 0 };
      current.status = response.status();
      current.count += 1;
      responses.set(safePath, current);
      if (response.status() === 401 || response.status() === 407) challengeResponses += 1;
    });

    const userAgent = await page.evaluate(() => navigator.userAgent);
    if (!browserUserAgentMatches(options.browser, userAgent)) {
      throw new HarnessError('browser_channel_mismatch', 'The launched browser user agent did not match the requested channel.');
    }
    evidence.browser.userAgentVerified = true;

    let navigation;
    try {
      navigation = await page.goto(startUrl.toString(), { waitUntil: 'domcontentloaded', timeout: options.timeoutMs });
    } catch (error) {
      throw new HarnessError('support_hub_unavailable', 'The exact Support Hub origin did not return a browser page.');
    }
    evidence.checks.page = { status: navigation?.status() ?? null, origin: supportHubOrigin };
    if (!navigation || navigation.status() >= 400) {
      throw new HarnessError('support_hub_unavailable', 'The exact Support Hub origin did not return a browser page.');
    }

    try {
      await page.waitForSelector('main[aria-label="POS Maintenance service control and evidence"]', { state: 'visible' });
    } catch {
      throw new HarnessError('support_hub_surface_unavailable', 'The POS Maintenance surface did not render at the exact Support Hub origin.');
    }

    const startWait = Date.now();
    while (Date.now() - startWait < options.timeoutMs) {
      const allPathsReceived = EXPECTED_PROTECTED_PATHS.every(pathName => responses.has(pathName));
      if (allPathsReceived) break;
      await page.waitForTimeout(200);
    }
    await page.waitForTimeout(500);

    const protectedReads = {};
    for (const protectedPath of EXPECTED_PROTECTED_PATHS) {
      const response = responses.get(protectedPath);
      protectedReads[protectedPath] = response ? { status: response.status, count: response.count } : { status: null, count: 0 };
    }
    evidence.checks.protectedReads = protectedReads;
    evidence.checks.browserAuthentication = {
      status: protectedReads['/api/v1/session'].status === 200 ? 'authenticated' : 'not-confirmed',
      challengeResponses
    };

    try {
      await page.waitForSelector('text="Local Administrator authorized"', { timeout: 5000 });
    } catch {
      // let authorization check below capture actual count
    }

    evidence.checks.authorization = {
      authenticatedLabel: await page.getByText('Windows authenticated', { exact: true }).count() > 0,
      administratorAuthorizedLabel: await page.getByText('Local Administrator authorized', { exact: true }).count() > 0
    };

    if (options.allowDisposableServiceAction) {
      evidence.checks.serviceControl = await executeDisposableServiceAction(page, agentOrigin, options.serviceId, responses);
      if (evidence.checks.serviceControl.status === 'outcome-unknown') {
        throw new HarnessError('service_action_outcome_unknown', 'The disposable service action outcome was unknown; no retry was attempted.');
      }
      if (evidence.checks.serviceControl.status !== 'accepted') {
        throw new HarnessError('service_action_not_accepted', 'The disposable service action was not accepted.');
      }
    }

    const finalCookies = await context.cookies();
    evidence.checks.disposableProfile.cookiesAfterNavigation = finalCookies.length;
    const allReadsSucceeded = EXPECTED_PROTECTED_PATHS.every(pathName => protectedReads[pathName].status === 200);
    if (!allReadsSucceeded || !evidence.checks.authorization.authenticatedLabel || !evidence.checks.authorization.administratorAuthorizedLabel) {
      throw new HarnessError('protected_reads_not_confirmed', 'The browser did not confirm every protected read and local authorization label.');
    }
    evidence.result = 'pass';
  } catch (error) {
    evidence.error = safeError(error);
  } finally {
    if (context) await context.close().catch(() => undefined);
    if (profileDirectory) await fs.rm(profileDirectory, { recursive: true, force: true }).catch(() => undefined);
    await fs.mkdir(path.dirname(options.output), { recursive: true });
    await fs.writeFile(options.output, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
  }

  return evidence;
}

async function executeDisposableServiceAction(page, agentOrigin, serviceId, responses) {
  const result = await page.evaluate(async ({ origin, targetId }) => {
    const servicesResponse = await fetch(`${origin}/api/v1/services`, { credentials: 'include', headers: { Accept: 'application/json' } });
    if (!servicesResponse.ok) return { status: 'service-read-failed', serviceReadStatus: servicesResponse.status };
    const services = await servicesResponse.json();
    const target = Array.isArray(services) ? services.find(item => item?.serviceId === targetId) : null;
    if (!target) return { status: 'opaque-target-not-found', serviceReadStatus: servicesResponse.status };

    const allowed = Array.isArray(target.allowedActions) ? target.allowedActions : [];
    const action = target.state === 'running' && allowed.includes('restart')
      ? 'restart'
      : target.state === 'stopped' && allowed.includes('start')
        ? 'start'
        : null;
    if (!action) return { status: 'state-valid-action-unavailable', serviceReadStatus: servicesResponse.status };

    const tokenResponse = await fetch(`${origin}/api/v1/security/mutation-token`, {
      method: 'POST',
      credentials: 'include',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify({ operationId: 'services.control', targetId })
    });
    if (!tokenResponse.ok) return { status: 'token-rejected', serviceReadStatus: servicesResponse.status, tokenStatus: tokenResponse.status };
    const tokenBody = await tokenResponse.json();
    if (typeof tokenBody?.token !== 'string' || tokenBody.token.length === 0) {
      return { status: 'token-response-invalid', serviceReadStatus: servicesResponse.status, tokenStatus: tokenResponse.status };
    }

    const actionResponse = await fetch(`${origin}/api/v1/services/${encodeURIComponent(targetId)}/actions`, {
      method: 'POST',
      credentials: 'include',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
        'X-RMS-Mutation-Token': tokenBody.token
      },
      body: JSON.stringify({ action, idempotencyKey: `support-int13c-${crypto.randomUUID()}` })
    });
    let outcome = 'notAttempted';
    const validOutcomes = new Set(['accepted', 'failed', 'outcomeUnknown', 'notAttempted']);
    if (actionResponse.ok) {
      const body = await actionResponse.json();
      if (validOutcomes.has(body?.outcome)) outcome = body.outcome;
    }
    return { status: outcome, serviceReadStatus: servicesResponse.status, tokenStatus: tokenResponse.status, actionStatus: actionResponse.status, action };
  }, { origin: agentOrigin, targetId: serviceId });

  return {
    attempted: true,
    opaqueTargetId: serviceId,
    status: result.status === 'outcomeUnknown' ? 'outcome-unknown' : result.status,
    serviceReadStatus: result.serviceReadStatus ?? null,
    tokenStatus: result.tokenStatus ?? null,
    actionStatus: result.actionStatus ?? null,
    action: result.action ?? null,
    responsePathObserved: responses.has('/api/v1/services/{opaque}/actions')
  };
}

run()
  .then(evidence => {
    if (evidence.result !== 'pass') process.exitCode = 2;
  })
  .catch(async error => {
    const output = process.argv.includes('--output')
      ? process.argv[process.argv.indexOf('--output') + 1]
      : path.join(process.cwd(), 'pos-browser-evidence.json');
    const failure = {
      schemaVersion: 1,
      result: 'blocked',
      generatedUtc: new Date().toISOString(),
      error: safeError(error)
    };
    await fs.mkdir(path.dirname(output), { recursive: true });
    await fs.writeFile(output, `${JSON.stringify(failure, null, 2)}\n`, 'utf8');
    process.exitCode = 2;
  });
