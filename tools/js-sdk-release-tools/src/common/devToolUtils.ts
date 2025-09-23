
import { logger } from '../utils/logger.js';
import * as path from 'path';
import { exists } from 'fs-extra';
import { runCommand, runCommandOptions } from './utils.js';

export async function formatSdk(packageDirectory: string) {
    logger.info(`Start to format code in '${packageDirectory}'.`);
    const hasSampleFolder = await exists(path.join(packageDirectory, "samples-dev"));
    const samplesDev = hasSampleFolder ? ` "samples-dev/**/*.ts"` : '';
    const hasTestFolder = await exists(path.join(packageDirectory, "test"));
    const test = hasTestFolder ? ` "test/**/*.{ts,cts,mts}"` : '';
    const cwd = packageDirectory;
    const options = { ...runCommandOptions, cwd };
    const formatCommand = `run vendored prettier --write --config ../../../.prettierrc.json --ignore-path ../../../.prettierignore "src/**/*.{ts,cts,mts}"${test} "*.{js,cjs,mjs,json}"${samplesDev}`;

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

export async function lintFix(packageDirectory: string) {
    const hasSampleFolder = await exists(path.join(packageDirectory, "samples-dev"));
    const samplesDev = hasSampleFolder ? ' samples-dev' : '';
    const hasTestFolder = await exists(path.join(packageDirectory, "test"));
    const test = hasTestFolder ? ' test' : '';
    const cwd = packageDirectory;
    const options = { ...runCommandOptions, cwd };

    logger.info("Start to build @azure/eslint-plugin-azure-sdk package to install eslint dependency.");
    await runCommand('pnpm', ['turbo', 'build', '--filter', `@azure/eslint-plugin-azure-sdk...`, '--token 1'], runCommandOptions);
    logger.info("Build @azure/eslint-plugin-azure-sdk package successfully.");
    logger.info(`Start to fix lint errors in '${packageDirectory}'.`);
    const lintFixCommand = `run vendored eslint package.json api-extractor.json src${test}${samplesDev} --fix --fix-type [problem,suggestion]`;

    try {
        await runCommand(`npm`, ['exec', '--', 'dev-tool', lintFixCommand], options, true, 1200, true);
        logger.info(`Fix the automatically repairable lint errors successfully.`);
    } catch (error) {
        logger.warn(`Failed to fix lint errors due to: ${(error as Error)?.stack ?? error}`);
    }
}

export async function customizeCodes(packageDirectory: string) {
    logger.info(`Start to customize codes in '${packageDirectory}'.`);
    const cwd = packageDirectory;
    const options = { ...runCommandOptions, cwd };

    try {
        //TODO: support ./src/generated cases in future
        const customizeCommand = `customization apply-v2 -s ./generated -c ./src`;
        await runCommand('npm', ['exec', '--', 'dev-tool', customizeCommand], options, true, 600, true);
        logger.info(`Customize codes successfully.`);
    } catch (error) {
        logger.warn(`Failed to customize codes due to: ${(error as Error)?.stack ?? error}`);
    }
}