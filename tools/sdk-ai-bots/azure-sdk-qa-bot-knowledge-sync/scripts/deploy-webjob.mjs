import { execFileSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { pathToFileURL } from 'node:url';

// Only these deliberately safe messages may be printed by the entry point.
class DeploymentError extends Error {}

// All subprocess output stays private; do not log tokens or appsettings responses.
function az(args) {
    const operation = args.slice(0, args.indexOf('--name') === -1 ? 2 : args.indexOf('--name')).join(' ');
    try {
        return execFileSync('az', [...args, '--only-show-errors', '--output', 'json'], {
            encoding: 'utf8', timeout: 120000, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 4 * 1024 * 1024
        });
    } catch (error) {
        throw new DeploymentError(`Azure CLI ${operation} failed (exit status ${error.status ?? 'unavailable'})`);
    }
}
async function request(url, token, { method = 'GET', body, contentType = 'application/json', allowMissing = false, headers = {} } = {}) {
    const operation = `${method} ${new URL(url).pathname}`;
    const started = Date.now();
    let response;
    try {
        response = await fetch(url, { method, body, redirect: 'error', signal: AbortSignal.timeout(120000),
            headers: { ...headers, Authorization: `Bearer ${token}`, 'Content-Type': contentType } });
    } catch {
        throw new DeploymentError(`SCM ${operation} failed before receiving an HTTP response; check SCM availability after restart and network access`);
    }
    if (allowMissing && response.status === 404) return null;
    if (!response.ok) {
        const error = new DeploymentError(`SCM ${operation} rejected: HTTP ${response.status}`);
        // Kudu may suppress the error body; its forwarding timeout is approximately 100 seconds.
        // Never print an arbitrary SCM error body or recover from an authorization failure.
        if (response.status === 400 && method === 'PUT' && contentType === 'application/zip') {
            const text = await response.text();
            error.forwardingTimeout = /A task was canceled\./i.test(text) || Date.now() - started >= 95000;
        }
        throw error;
    }
    try {
        const text = await response.text();
        return text ? JSON.parse(text) : null;
    } catch {
        throw new DeploymentError(`SCM ${operation} returned an unreadable or invalid JSON response (HTTP ${response.status})`);
    }
}
async function waitForUpload(job, token) {
    const name = new URL(job).pathname.split('/').pop();
    const deadline = Date.now() + 5 * 60 * 1000;
    while (Date.now() < deadline) {
        const installed = await request(job, token, { allowMissing: true });
        if (installed?.name === name && !installed.error) return;
        await delay(15000);
    }
    throw new DeploymentError('Timed-out upload did not produce a discoverable WebJob within 5 minutes');
}
async function assertPausedAndDrained(job, token) {
    const paused = await request(job, token);
    if (!paused?.settings || paused.settings.schedule) throw new DeploymentError('Job schedule was not removed');
    const history = await request(`${job}/history`, token);
    if (!Array.isArray(history?.runs) || history.runs.some(run => !['Success', 'Failed', 'Aborted'].includes(run.status))) {
        throw new DeploymentError('Job has running/pending history or unknown state; drain before deployment');
    }
}
export async function deploy(env = process.env) {
    for (const name of ['WEBJOB_ZIP', 'APP_NAME', 'RESOURCE_GROUP', 'WEBJOB_NAME']) {
        if (typeof env[name] !== 'string' || !env[name].trim()) throw new DeploymentError(`Required deployment input missing: ${name}`);
    }
    if (!/^[a-zA-Z0-9][a-zA-Z0-9_-]*$/.test(env.WEBJOB_NAME ?? '')) throw new DeploymentError('Invalid WebJob name');
    const archive = await readFile(env.WEBJOB_ZIP);
    if (!archive.length) throw new DeploymentError('WebJob ZIP is empty');
    const settings = JSON.parse(await readFile(new URL('../webjob/settings.job', import.meta.url), 'utf8'));
    const target = ['--name', env.APP_NAME, '--resource-group', env.RESOURCE_GROUP];
    const site = JSON.parse(az(['webapp', 'show', ...target]));
    if (!site.reserved || !site.kind?.includes('container')) throw new DeploymentError('Expected Linux container App Service');
    // Read only the required existing app settings and scheduler timezone; never replace app identity/configuration.
    const existingSettings = JSON.parse(az(['webapp', 'config', 'appsettings', 'list', ...target,
        '--query', "[?name=='AZURE_APPCONFIG_ENDPOINT' || name=='AZURE_CLIENT_ID' || name=='WEBSITE_TIME_ZONE' || name=='TZ'].{name:name,value:value}"]));
    for (const name of ['AZURE_APPCONFIG_ENDPOINT', 'AZURE_CLIENT_ID']) {
        if (!existingSettings.some(setting => setting.name === name && typeof setting.value === 'string' && setting.value.trim())) {
            throw new DeploymentError(`Required existing app setting missing: ${name}`);
        }
    }
    if (existingSettings.some(setting => ['WEBSITE_TIME_ZONE', 'TZ'].includes(setting.name) && !['UTC', 'Etc/UTC'].includes(setting.value))) {
        throw new DeploymentError('Scheduler must use UTC; timezone settings are not modified');
    }
    const scmHost = site.enabledHostNames?.find(host => host.includes('.scm.') && host.endsWith('.azurewebsites.net'));
    if (!scmHost || !/^[a-z0-9.-]+$/i.test(scmHost)) throw new DeploymentError('SCM hostname not available');
    const token = JSON.parse(az(['account', 'get-access-token'])).accessToken;
    if (!token) throw new DeploymentError('SCM Entra token unavailable');
    const job = `https://${scmHost}/api/triggeredwebjobs/${encodeURIComponent(env.WEBJOB_NAME)}`;
    const existing = await request(job, token, { allowMissing: true });
    // Pause only this job, never all WebJobs on the backend.
    if (existing) {
        await request(`${job}/settings`, token, { method: 'PUT', body: JSON.stringify({ ...settings, schedule: null, run_mode: 'on_demand' }) });
        await assertPausedAndDrained(job, token);
    }
    console.warn('Enabling Always On, the Kudu agent and persistent App Service storage may restart the backend app.');
    az(['webapp', 'config', 'set', ...target, '--always-on', 'true']);
    az(['webapp', 'config', 'appsettings', 'set', ...target, '--settings',
        'WEBSITE_SKIP_RUNNING_KUDUAGENT=false', 'WEBSITES_ENABLE_APP_SERVICE_STORAGE=true']);
    // Configuration changes can restart the app; recheck immediately before overwriting this job.
    if (existing) await assertPausedAndDrained(job, token);
    try {
        await request(job, token, { method: 'PUT', contentType: 'application/zip', body: archive,
            headers: { 'Content-Disposition': 'attachment; filename="knowledge-sync.zip"' } });
    } catch (error) {
        if (!error.forwardingTimeout) throw error;
        console.warn('Kudu upload forwarding timed out; waiting for WebJob discovery.');
        await waitForUpload(job, token);
    }
    const installed = await request(job, token);
    if (installed?.name !== env.WEBJOB_NAME || installed.error) throw new DeploymentError('Job discovery failed');
    await request(`${job}/settings`, token, { method: 'PUT', body: JSON.stringify(settings) });
    const final = await request(job, token);
    if (final?.name !== env.WEBJOB_NAME || final.error) throw new DeploymentError('Job discovery failed');
    if (final.settings?.schedule !== settings.schedule || final.settings?.is_singleton !== true) throw new DeploymentError('Schedule verification failed');
    console.log(JSON.stringify({ job: env.WEBJOB_NAME, schedule: settings.schedule }));
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    deploy().catch(error => {
        console.error(`WebJob deployment failed: ${error instanceof DeploymentError ? error.message : 'Unexpected error; raw details suppressed to protect credentials'}. No basic-auth fallback.`);
        process.exitCode = 1;
    });
}