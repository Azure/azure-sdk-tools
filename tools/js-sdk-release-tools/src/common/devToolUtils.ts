
import { logger } from '../utils/logger.js';
import * as path from 'path';
import { exists } from 'fs-extra';
import { runCommand, runCommandOptions } from './utils.js';

export async function formatSdk(packageDirectory: string) {
    logger.info(`Start to format code in '${packageDirectory}'.`);
    const cwd = packageDirectory;
    const options = { ...runCommandOptions, cwd };
    const formatCommand = 'run vendored prettier --write --config ../../../.prettierrc.json --ignore-path ../../../.prettierignore \"src/**/*.{ts,cts,mts}\" \"test/**/*.{ts,cts,mts}\" \"*.{js,cjs,mjs,json}\" \"samples-dev/*.ts\"';

    try {
        await runCommand(`npm`, ['exec', '--', 'dev-tool', formatCommand], options, true, 300, true);
        logger.info(`format sdk successfully.`);
    } catch (error) {
        logger.warn(`Failed to format code due to: ${(error as Error)?.stack ?? error}`);
    }

}

export async function updateSnippets(packageDirectory: string) {
    logger.info(`Start to update snippets in '${packageDirectory}'.`);
    const cwd = packageDirectory;
    const options = { ...runCommandOptions, cwd };

    try {
        const updateCommand = 'run update-snippets';
        await runCommand('npm', ['exec', '--', 'dev-tool', updateCommand], options, true, 300, true);
        logger.info(`Snippets updated successfully.`);
    } catch (error) {
        logger.warn(`Failed to update snippets due to: ${(error as Error)?.stack ?? error}`);
    }
}

export async function lintFix(sdkPath:string, packageDirectory: string) {
    const hasSampleFolder = await exists(path.join(packageDirectory, "samples-dev"));
    const samplesDev = hasSampleFolder ? ` ${path.join(packageDirectory, "samples-dev")}` : '';
    logger.info(`Start to fix lint errors in '${packageDirectory}'.`);
    const cwd = sdkPath;
    const options = { ...runCommandOptions, cwd };

    const lintFixCommand = `run vendored eslint ${path.join(packageDirectory, "package.json")} ${path.join(packageDirectory, "api-extractor.json")} ${path.join(packageDirectory, "src")} ${path.join(packageDirectory, "test")}${samplesDev} --fix --fix-type [problem,suggestion]`;

    try {
        await runCommand(`npm`, ['exec', '--', 'dev-tool', lintFixCommand], options, true, 300, true);
        logger.info(`Fix the automatically repairable lint errors successfully.`);
    } catch (error) {
        logger.warn(`Failed to fix lint errors due to: ${(error as Error)?.stack ?? error}`);
    }
}