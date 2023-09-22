import * as fs from 'fs';
import * as path from 'path';
import winston from 'winston';

export function extractAutorestConfigs(autorestConfigFilePath: string, sdkRepo: string, logger: winston.Logger): string | undefined {
    if (!fs.existsSync(autorestConfigFilePath)) {
        return undefined;
    }
    const autorestConfigFileContent = fs.readFileSync(autorestConfigFilePath, 'utf-8');
    try {
        let autorestConfigs: string[] = autorestConfigFileContent.split(/#+ *azure-sdk-for-/);
        if (autorestConfigs.length < 2) {
            throw new Error(`Parse autorest config file failed.`);
        }
        autorestConfigs = autorestConfigs.slice(1);
        if (!path.basename(sdkRepo).startsWith('azure-sdk-for-')) {
            if (autorestConfigs.length > 1) {
                logger.warn(`Docker is running in pipeline, but get autorest config for more than 1 language of sdk. So only get the first autorest config`);
            }
            return `# azure-sdk-for-${autorestConfigs[0]}`.replace(/\r\n/g, '\n').replace(/\n/g, '\r\n');
        }
        for (const autorestConfig of autorestConfigs) {
            let autorestFullConfig = `# azure-sdk-for-${autorestConfig}`;
            if (autorestFullConfig.startsWith(`# ${path.basename(sdkRepo)}`)) {
                logger.info(`Find autorest config for ${path.basename(sdkRepo)} in ${autorestConfigFilePath}: \n${autorestFullConfig}`);
                autorestFullConfig = autorestFullConfig.replace(/\r\n/g, '\n').replace(/\n/g, '\r\n');
                return autorestFullConfig;
            }
        }
    } catch (e) {
        logger.error(`Parse ${autorestConfigFilePath} failed: ${e.message}`);
        throw e;
    }
    logger.warn(`Cannot find autorest config for ${path.basename(sdkRepo)} in ${autorestConfigFilePath}.`);
    return undefined;
}
