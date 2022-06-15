import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import winston from 'winston';

export function extractAutorestConfigs(autorestConfigFilePath: string, sdkRepo: string, logger: winston.Logger): string | undefined {
    if (!fs.existsSync(autorestConfigFilePath)) {
        return undefined;
    }
    const autorestConfigFileContent = fs.readFileSync(autorestConfigFilePath, 'utf-8');
    try {
        const autorestConfigs: string[] = autorestConfigFileContent.split(/#+ *azure-sdk-for-/);
        for (const autorestConfig of autorestConfigs) {
            let autorestFullConfig = `# azure-sdk-for-${autorestConfig}`;
            if (autorestFullConfig.startsWith(`# ${path.basename(sdkRepo)}`)) {
                logger.info(`Find autorest config for ${path.basename(sdkRepo)} in ${autorestConfigFilePath}: \n${autorestFullConfig}`);
                if (os.EOL == '\n') {
                    autorestFullConfig = autorestFullConfig.replace(/\n/g, '\r\n');
                }
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
