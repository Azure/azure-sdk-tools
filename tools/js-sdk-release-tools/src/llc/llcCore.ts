import * as path from "path";
import {logger} from "../utils/logger";
import {execSync} from "child_process";
import {generateTsConfig} from "./generateTsConfig";
import {generatePackageJson} from "./generatePackageJson";
import {generateRollupConfig} from "./generateRollupConfig";
import {generateApiExtractorConfig} from "./generateApiExtractorConfig";
import {generateLinterConfig} from "./generateLinterConfig";
import {generateLicense} from "./generateLicense";
import {generateTest} from "./generateTest";
import {generateSample} from "./generateSample";
import {getRelativePackagePath} from "./utils";
import {generateChangelog} from "./generateChangelog";
import {hackByModifyConfig, ModifyModel} from "./hackByModifyConfig";
import {generateKarmaConfig} from "./generateKarmaConfig";
import {changeRushJson} from "../utils/changeRushJson";
import {modifyOrGenerateCiYaml} from "../utils/changeCiYaml";
import {generateReadmeMd} from "./generateReadmeMd";

const shell = require('shelljs')

export async function generateCodes(sdkRepo: string, packagePath: string, packageName: string) {
    let cmd = `autorest  --typescript README.md`;
    try {
        shell.cd(path.join(packagePath, 'swagger'));
        logger.logGreen('Executing command:');
        logger.logGreen('------------------------------------------------------------');
        logger.logGreen(cmd);
        logger.logGreen('------------------------------------------------------------');
        execSync(cmd, {stdio: 'inherit'});
        logger.logGreen(`Generating config files`);
        shell.cd(packagePath);
        await generateTsConfig(packagePath, packageName);
        await generatePackageJson(packagePath, packageName, sdkRepo);
        await generateRollupConfig(packagePath);
        await generateApiExtractorConfig(packagePath, packageName);
        await generateLinterConfig(packagePath);
        await generateLicense(packagePath);
        await generateReadmeMd(packagePath, packageName)
        await generateTest(packagePath);
        await generateKarmaConfig(packagePath);
        await generateSample(packagePath);
        await modifyOrGenerateCiYaml(sdkRepo, packagePath, packageName, false);
        await changeRushJson(sdkRepo, packageName, getRelativePackagePath(packagePath), 'client');
    } catch (e) {
        logger.logError('Error:');
        logger.logError(`An error occurred while generating codes in ${packagePath}: ${e.stack}`);
        process.exit(1);
    }
}

export async function buildGeneratedCodes(sdkrepo: string, packagePath: string, packageName: string) {
    try {
        shell.cd(sdkrepo);
        logger.logGreen(`rush update`);
        execSync('rush update', {stdio: 'inherit'});
        logger.logGreen(`rush build -t ${packageName}: Build generated codes, except test and sample, which may be written manually`);
        // To build generated codes except test and sample, we need to change tsconfig.json.
        hackByModifyConfig(packagePath, ModifyModel.Change);
        execSync(`rush build -t ${packageName}`, {stdio: 'inherit'});
        hackByModifyConfig(packagePath, ModifyModel.Revert);
        shell.cd(packagePath);
        logger.logGreen(`Generate changelog`);
        await generateChangelog(packagePath);
        logger.logGreen(`Clean compiled outputs`);
        execSync('rushx clean', {stdio: 'inherit'});
    } catch (e) {
        logger.logError(`Build failed: ` + e.message);
        process.exit(1);
    }
}
