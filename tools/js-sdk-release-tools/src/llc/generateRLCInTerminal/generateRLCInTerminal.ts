import * as path from "path";
import {logger} from "../../utils/logger";
import {execSync} from "child_process";
import {generateChangelog} from "../utils/generateChangelog";
import {changeConfigOfTestAndSample, ChangeModel, SdkType} from "../../utils/changeConfigOfTestAndSample";
import {generateExtraFiles} from "../utils/generateExtraFiles";

const shell = require('shelljs')

export async function generateCodes(sdkRepo: string, packagePath: string, packageName: string) {
    let cmd = `autorest  --typescript README.md`;
    shell.cd(path.join(packagePath, 'swagger'));
    logger.info('Executing command:');
    logger.info('------------------------------------------------------------');
    logger.info(cmd);
    logger.info('------------------------------------------------------------');
    execSync(cmd, {stdio: 'inherit'});
    logger.info(`Generating config files`);
    shell.cd(packagePath);
    await generateExtraFiles(packagePath, packageName, sdkRepo);
}

export async function buildGeneratedCodes(sdkrepo: string, packagePath: string, packageName: string) {
    shell.cd(sdkrepo);
    logger.info(`rush update`);
    execSync('rush update', {stdio: 'inherit'});
    logger.info(`rush build -t ${packageName}: Build generated codes, except test and sample, which may be written manually`);
    // To build generated codes except test and sample, we need to change tsconfig.json.
    changeConfigOfTestAndSample(packagePath, ChangeModel.Change, SdkType.Rlc);
    execSync(`rush build -t ${packageName}`, {stdio: 'inherit'});
    changeConfigOfTestAndSample(packagePath, ChangeModel.Revert, SdkType.Rlc);
    shell.cd(packagePath);
    logger.info(`Generate changelog`);
    await generateChangelog(packagePath);
    logger.info(`Clean compiled outputs`);
    execSync('rushx clean', {stdio: 'inherit'});
}
