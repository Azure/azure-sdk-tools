import { ModularClientPackageOptions } from '../../../common/types';
import { getGeneratedPackageDirectory, runCommand, runCommandOptions } from '../../../common/utils';
import { logger } from '../../../utils/logger';
import { join, posix } from 'node:path';
import { readFile } from 'node:fs/promises';
import { parse } from 'yaml';

// ./eng/common/scripts/TypeSpec-Project-Process.ps1 script forces to use emitter '@azure-tools/typespec-ts'
export async function generateTypeScriptCodeFromTypeSpec(options: ModularClientPackageOptions): Promise<string> {
    const script = './eng/common/scripts/TypeSpec-Project-Process.ps1';
    try {
        await runCommand(
            'pwsh',
            [script, options.typeSpecDirectory, options.gitCommitId, options.repoUrl],
            runCommandOptions
        );
        logger.logInfo(`Generated typescript code successfully.`);

        return getGeneratedPackageDirectory(options.typeSpecDirectory);
    } catch (err) {
        logger.logError(`Run command failed due to: ${(err as Error)?.stack ?? err}`);
        throw err;
    }
}
