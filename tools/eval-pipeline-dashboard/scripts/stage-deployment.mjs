import { cp, mkdir, readdir } from "node:fs/promises";
import { join, resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const target = process.argv[2] ? resolve(process.argv[2]) : null;
if (!target || target === root || target.startsWith(join(root, "node_modules"))) {
  throw new Error("Provide a separate deployment staging directory.");
}
await mkdir(target, { recursive: true });
if ((await readdir(target)).length) throw new Error("Deployment staging must be empty; existing data will not be overwritten.");
// Explicit allowlist: no local result archives, receipt/query DBs, credentials,
// ignored fixtures, test emulator or repo files may enter the deployment artifact.
for (const name of ["package.json", "package-lock.json", "start.js", "server.js", "pipelines.js", "lib"]) {
  await cp(join(root, name), join(target, name), { recursive: true, dereference: false });
}
await mkdir(join(target, "scripts"));
for (const name of ["patch-vally.mjs", "maintenance.mjs", "publish-bundle.mjs", "prepare-pipeline-bundle.mjs", "check-deployment-target.mjs"]) {
  await cp(join(root, "scripts", name), join(target, "scripts", name));
}
console.log(`Staged deployment source in ${target}. Restore production dependencies on the target OS before packaging.`);