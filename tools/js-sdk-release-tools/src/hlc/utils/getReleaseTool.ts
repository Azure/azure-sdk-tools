
import { readFileSync } from 'fs';
import { resolve } from 'path';


export function getReleaseTool() {
    const __dirname = import.meta.dirname;
    const packageJsonPath = resolve(__dirname, '..', '..', '..', 'package.json');
    const packageJson = JSON.parse(readFileSync(packageJsonPath, 'utf-8'));
    const { name, version } = packageJson;

    return `${name}@${version}`;
}
