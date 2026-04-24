
import { logger } from '../utils/logger.js';
import { runCommand, runCommandOptions } from './utils.js';

export async function formatSdk(packageDirectory: string) {
    logger.info(`Start to format code in '${packageDirectory}'.`);
    const cwd = packageDirectory;
    const options = { ...runCommandOptions, cwd };

    try {
        await runCommand(`npm`, ['run', 'format'], options, true, 300, true);
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
    logger.info(`Start to fix lint errors in '${packageDirectory}'.`);
    const cwd = packageDirectory;
    const options = { ...runCommandOptions, cwd };

    try {
        await runCommand(`npm`, ['run', 'lint:fix'], options, true, 3600, true);
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