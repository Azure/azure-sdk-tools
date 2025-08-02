import envPaths from "env-paths";
import * as fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const pkgPath = path.resolve(__dirname, "../../package.json");
const pkg = JSON.parse(await fs.readFile(pkgPath, "utf-8"));
export const appEnvPaths = envPaths(pkg.name);
