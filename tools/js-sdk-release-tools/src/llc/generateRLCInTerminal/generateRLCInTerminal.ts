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
    logger.logGreen('Executing command:');
    logger.logGreen('------------------------------------------------------------');
    logger.logGreen(cmd);
    logger.logGreen('------------------------------------------------------------');
    execSync(cmd, {stdio: 'inherit'});
    logger.logGreen(`Generating config files`);
    shell.cd(packagePath);
    await generateExtraFiles(packagePath, packageName, sdkRepo);
}

export async function buildGeneratedCodes(sdkrepo: string, packagePath: string, packageName: string) {
    shell.cd(sdkrepo);
    logger.logGreen(`rush update`);
    execSync('rush update', {stdio: 'inherit'});
    logger.logGreen(`rush build -t ${packageName}: Build generated codes, except test and sample, which may be written manually`);
    // To build generated codes except test and sample, we need to change tsconfig.json.
    changeConfigOfTestAndSample(packagePath, ChangeModel.Change, SdkType.Rlc);
    execSync(`rush build -t ${packageName}`, {stdio: 'inherit'});
    changeConfigOfTestAndSample(packagePath, ChangeModel.Revert, SdkType.Rlc);
    shell.cd(packagePath);
    logger.logGreen(`Generate changelog`);
    await generateChangelog(packagePath);
    logger.logGreen(`Clean compiled outputs`);
    execSync('rushx clean', {stdio: 'inherit'});
}
