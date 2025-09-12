#!/usr/bin/env ts-node
/**
 * Sync all *.user environment files under ./env to an Azure Key Vault as secrets.
 *
 * Each .env.<env>.user file becomes ONE secret: BOT_SERVICE_<ENV>
 * Where:
 *   - BOT_SERVICE: hard-coded prefix
 *   - ENV: derived from filename .env.<env>.user => <env>, uppercased
 * Secret value is the full raw content of the file (as-is).
 *
 * Comments (# ...) and blank lines ignored. If a value is quoted, quotes are trimmed.
 * Existing secrets are always overwritten (new version created in Key Vault).
 *
 * Usage:
 *   ts-node scripts/syncCredentials/syncToKeyVault.ts --vault <vaultName>
 *
 * Requirements:
 *   - Azure CLI logged in (az login)
 *   - Access policy to set secrets on the target Key Vault
 */
import { promises as fsp } from 'fs';
import * as path from 'path';
import readline from 'readline';
import { DefaultAzureCredential } from '@azure/identity';
import { SecretClient } from '@azure/keyvault-secrets';

const DEFAULT_VAULT = 'AzureSDKQABotConfig';
const FIXED_PREFIX = 'BOT-SERVICE';

interface Options {
  vault: string; // resolved vault name (hardcoded default)
  envDir: string;
}

// Per-file mode: one secret per .user env file.

function printUsageAndExit(msg?: string, code = 1) {
  if (msg) console.error('Error: ' + msg);
  console.log(`\nUsage: syncToKeyVault.ts [--vault <vaultName>] [--envDir ./env]`);
  console.log(`       (default vault: ${DEFAULT_VAULT})`);
  console.log(`       (prefix fixed as ${FIXED_PREFIX})`);
  process.exit(code);
}

function parseArgs(argv: string[]): Options {
  const args = argv.slice(2);
  let vault: string | undefined;
  let envDir = path.resolve('env');
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a === '--vault') vault = args[++i] || '';
    else if (a === '--envDir') envDir = path.resolve(args[++i] || envDir);
    else if (a === '-h' || a === '--help') printUsageAndExit();
    else printUsageAndExit('Unknown argument: ' + a);
  }
  const resolvedVault = vault && vault.trim().length > 0 ? vault : DEFAULT_VAULT;
  return { vault: resolvedVault, envDir };
}

async function listUserEnvFiles(envDir: string): Promise<string[]> {
  const entries = await fsp.readdir(envDir, { withFileTypes: true });
  return entries.filter((e) => e.isFile() && /\.env\.[^.]+\.user$/i.test(e.name)).map((e) => path.join(envDir, e.name));
}

function deriveEnvName(filename: string): string {
  const m = filename.match(/\.env\.([^.]+)\.user$/i);
  return (m ? m[1] : 'env').toUpperCase();
}

// Use @azure/keyvault-secrets SDK
const credential = new DefaultAzureCredential();

function buildSecretClient(vaultName: string): SecretClient {
  const vaultUrl = vaultName.startsWith('https://') ? vaultName : `https://${vaultName}.vault.azure.net`;
  return new SecretClient(vaultUrl, credential);
}

async function run() {
  const opts = parseArgs(process.argv);
  const client = buildSecretClient(opts.vault);
  console.log(`Target Key Vault: ${opts.vault}`);
  console.log(`Scanning env dir: ${opts.envDir}`);
  const files = await listUserEnvFiles(opts.envDir);
  if (files.length === 0) {
    console.log('No *.user env files found.');
    return;
  }
  console.log(`Found ${files.length} user env file(s).`);

  // Build plan with base64 content
  const plan = [] as { file: string; envName: string; secretName: string; content: string }[];
  for (const f of files) {
    const envName = deriveEnvName(path.basename(f));
    const secretName = `${FIXED_PREFIX}-${envName}`.toUpperCase();
    const content = await fsp.readFile(f, 'utf8');
    plan.push({ file: f, envName, secretName, content });
  }
  console.log('\nUpload plan (one secret per file, showing Base64 content):');
  for (const p of plan) {
    console.log('------------------------------------------------------------');
    console.log(`File:        ${path.basename(p.file)}`);
    console.log(`Secret Name: ${p.secretName}`);
    console.log('Content:');
    console.log(p.content);
  }
  console.log('------------------------------------------------------------');
  console.log('\nPress Enter to confirm upload, or Ctrl+C to cancel.');
  await waitForEnter();

  let total = 0;
  for (const p of plan) {
    console.log(`Start to set secret: ${p.secretName}`);
    await client.setSecret(p.secretName, p.content);
    console.log(`Set: ${p.secretName}`);
    total++;
  }
  console.log(`\nDone. Processed ${total} file secret(s).`);
}

run().catch((err) => {
  console.error('Failed:', err.message || err);
  process.exit(1);
});

async function waitForEnter(): Promise<void> {
  return new Promise((resolve) => {
    const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
    rl.question('', () => {
      rl.close();
      resolve();
    });
  });
}

export {}; // ensure module scope
