import { logger } from '../utils/logger.js';
import { runCommand, runCommandOptions } from './utils.js';
import fs from 'fs';
import path from 'path';

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
    // Ensure eslint is a dev dependency so `npm exec -- eslint` resolves it from local node_modules.
    // Mgmt packages set lint/lint:fix to "echo skipped", so we run eslint directly instead of
    // going through `npm run lint:fix` (which would be a no-op for those packages).
    const packageJsonPath = path.join(packageDirectory, 'package.json');
    const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, { encoding: 'utf-8' }));
    if (!packageJson.devDependencies?.eslint) {
      logger.info(`eslint not found in devDependencies, adding it.`);
      packageJson.devDependencies = packageJson.devDependencies ?? {};
      packageJson.devDependencies['eslint'] = '^8.0.0';
      fs.writeFileSync(packageJsonPath, JSON.stringify(packageJson, null, '  '), { encoding: 'utf-8' });
      logger.info(`Re-running pnpm install to install newly added eslint devDependency.`);
      await runCommand('pnpm', ['install', '--no-frozen-lockfile'], options, true, 300, true);
      logger.info(`pnpm install completed after adding eslint.`);
    } else {
      logger.info(`eslint found in devDependencies: ${packageJson.devDependencies.eslint}`);
    }

    // Ensure workspace packages used by the eslint config (e.g. @azure/eslint-plugin-azure-sdk)
    // are built before invoking eslint, since pnpm install does not build workspace packages.
    // Run from the process cwd (monorepo root) so pnpm can resolve the workspace filter.
    logger.info(`Building @azure/eslint-plugin-azure-sdk to ensure its dist files are available.`);
    await runCommand('pnpm', ['build', '--filter', '@azure/eslint-plugin-azure-sdk'], runCommandOptions, true, 300, true);
    logger.info(`@azure/eslint-plugin-azure-sdk build step completed.`);

    // Build the list of paths to lint; conditionally include test and samples-dev if they exist.
    const lintPaths = ['package.json', 'api-extractor.json', 'src'];
    if (fs.existsSync(path.join(packageDirectory, 'test'))) {
      lintPaths.push('test');
      logger.info(`'test' directory found, including in lint paths.`);
    }
    if (fs.existsSync(path.join(packageDirectory, 'samples-dev'))) {
      lintPaths.push('samples-dev');
      logger.info(`'samples-dev' directory found, including in lint paths.`);
    }
    logger.info(`Lint paths: ${lintPaths.join(', ')}`);

    await runCommand(
      'npm',
      ['exec', '--', 'eslint', ...lintPaths, '--fix', '--fix-type', '[problem,suggestion]'],
      options,
      true,
      3600,
      true
    );
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
