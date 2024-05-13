import * as fs from 'fs';
import * as path from 'path';
import { logger } from './logger';

export async function backupNodeModules(folder: string) {
    const nodeModulesPath = path.join(folder, "node_modules");
    if (fs.existsSync(nodeModulesPath)) {
        logger.logGreen(`rename ${nodeModulesPath} to ${nodeModulesPath}_backup`);
        fs.renameSync(nodeModulesPath, `${nodeModulesPath}_backup`);
    }
    if ('/' === path.dirname(folder)) return;
    await backupNodeModules(path.dirname(folder));
}

export async function restoreNodeModules(folder: string) {
    const nodeModulesPath = path.join(folder, "node_modules_backup");
    if (fs.existsSync(nodeModulesPath)) {
        logger.logGreen(`rename ${nodeModulesPath} to ${nodeModulesPath.replace('_backup', '')}`);
        fs.renameSync(nodeModulesPath, `${nodeModulesPath.replace('_backup', '')}`);
    }
    if ('/' === path.dirname(folder)) return;
    await restoreNodeModules(path.dirname(folder));
}
