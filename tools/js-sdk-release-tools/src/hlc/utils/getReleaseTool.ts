import { readFileSync } from "fs";
import * as path from "path";
import { fileURLToPath } from "url";

export function getReleaseTool() {
    const __dirname = path.dirname(fileURLToPath(import.meta.url));
    const packageJson = JSON.parse(readFileSync(path.resolve(__dirname, '..', '..', '..', 'package.json'), 'utf-8'));
    const { name, version } = packageJson;

    return `${name}@${version}`;
}
