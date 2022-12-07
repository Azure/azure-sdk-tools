import * as fs from 'fs';

export function copyPackageJsonFileIfNotExist(source: string, target: string) {
    if (fs.existsSync(target)) return;
    if (fs.existsSync(source)) {
        fs.copyFileSync(source, target);
    }
}
