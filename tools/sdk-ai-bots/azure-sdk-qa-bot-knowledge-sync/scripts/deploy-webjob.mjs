import { execFileSync } from 'node:child_process';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

// All subprocess output stays private; do not log tokens or appsettings responses.
function az(args) {
    return execFileSync('az', [...args, '--only-show-errors', '--output', 'json'], {
        encoding: 'utf8', timeout: 120000, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 4 * 1024 * 1024
    });
}
async function request(url, token, { method = 'GET', body, contentType = 'application/json', allowMissing = false } = {}) {
    const response = await fetch(url, { method, body, redirect: 'error', signal: AbortSignal.timeout(120000),
        headers: { Authorization: `Bearer ${token}`, 'Content-Type': contentType } });
    if (allowMissing && response.status === 404) return null;
    if (!response.ok) throw new Error(`Deployment API rejected ${method}: HTTP ${response.status}`);
    const text = await response.text();
    return text ? JSON.parse(text) : null;
}
async function assertPausedAndDrained(job, token) {
    const paused = await request(job, token);
    if (!paused?.settings || paused.settings.schedule) throw new Error('Job schedule was not removed');
    const history = await request(`${job}/history`, token);
    if (!Array.isArray(history?.runs) || history.runs.some(run => !['Success', 'Failed', 'Aborted'].includes(run.status))) {
        throw new Error('Job has running/pending history or unknown state; drain before deployment');
    }
}
export async function deploy(env = process.env) {
    for (const name of ['WEBJOB_ZIP', 'APP_NAME', 'RESOURCE_GROUP', 'WEBJOB_NAME']) {
        if (typeof env[name] !== 'string' || !env[name].trim()) throw new Error(`Required deployment input missing: ${name}`);
    }
    if (!/^[a-zA-Z0-9][a-zA-Z0-9_-]*$/.test(env.WEBJOB_NAME ?? '')) throw new Error('Invalid WebJob name');
    const archive = await readFile(env.WEBJOB_ZIP);
    if (!archive.length) throw new Error('WebJob ZIP is empty');
    const settings = JSON.parse(await readFile(new URL('../webjob/settings.job', import.meta.url), 'utf8'));
    const target = ['--name', env.APP_NAME, '--resource-group', env.RESOURCE_GROUP];
    const site = JSON.parse(az(['webapp', 'show', ...target]));
    if (!site.reserved || !site.kind?.includes('container')) throw new Error('Expected Linux container App Service');
    // Read only the required existing app settings and scheduler timezone; never replace app identity/configuration.
    const existingSettings = JSON.parse(az(['webapp', 'config', 'appsettings', 'list', ...target,
        '--query', "[?name=='AZURE_APPCONFIG_ENDPOINT' || name=='AZURE_CLIENT_ID' || name=='WEBSITE_TIME_ZONE' || name=='TZ'].{name:name,value:value}"]));
    for (const name of ['AZURE_APPCONFIG_ENDPOINT', 'AZURE_CLIENT_ID']) {
        if (!existingSettings.some(setting => setting.name === name && typeof setting.value === 'string' && setting.value.trim())) {
            throw new Error(`Required existing app setting missing: ${name}`);
        }
    }
    if (existingSettings.some(setting => ['WEBSITE_TIME_ZONE', 'TZ'].includes(setting.name) && !['UTC', 'Etc/UTC'].includes(setting.value))) {
        throw new Error('Scheduler must use UTC; timezone settings are not modified');
    }
    const scmHost = site.enabledHostNames?.find(host => host.includes('.scm.') && host.endsWith('.azurewebsites.net'));
    if (!scmHost || !/^[a-z0-9.-]+$/i.test(scmHost)) throw new Error('SCM hostname not available');
    const token = JSON.parse(az(['account', 'get-access-token'])).accessToken;
    if (!token) throw new Error('SCM Entra token unavailable');
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
    await request(job, token, { method: 'PUT', contentType: 'application/zip', body: archive });
    const installed = await request(job, token);
    if (installed?.name !== env.WEBJOB_NAME) throw new Error('Job discovery failed');
    await request(`${job}/settings`, token, { method: 'PUT', body: JSON.stringify(settings) });
    const final = await request(job, token);
    if (final?.name !== env.WEBJOB_NAME) throw new Error('Job discovery failed');
    if (final.settings?.schedule !== settings.schedule || final.settings?.is_singleton !== true) throw new Error('Schedule verification failed');
    console.log(JSON.stringify({ job: env.WEBJOB_NAME, schedule: settings.schedule }));
}
if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
    deploy().catch(() => { console.error('WebJob deployment failed closed; check deployment inputs, app settings, SCM Entra authorization and job history. No basic-auth fallback.'); process.exitCode = 1; });
}