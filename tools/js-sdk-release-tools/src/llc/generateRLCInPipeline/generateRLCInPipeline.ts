import {logger} from "../../utils/logger";
import * as path from "path";
import {execSync} from "child_process";
import {getChangedPackageDirectory} from "../../utils/git";
import {SwaggerSdkAutomationOutputPackageInfo} from "../../common-types/swaggerSdkAutomation";
import fs from "fs";
import {generateChangelog} from "../utils/generateChangelog";
import {changeConfigOfTestAndSample, ChangeModel, SdkType} from "../../utils/changeConfigOfTestAndSample";
import {generateExtraFiles} from "../utils/generateExtraFiles";

export async function generateRLCInPipeline(options: {
    sdkRepo: string;
    swaggerRepo: string;
    readmeMd: string;
    use?: string;
    outputJson?: any;
    additionalArgs?: string;
}) {
    logger.logGreen(`>>>>>>>>>>>>>>>>>>> Start: "${options.readmeMd}" >>>>>>>>>>>>>>>>>>>>>>>>>`);
    let cmd = `autorest --typescript --typescript-sdks-folder=${options.sdkRepo} ${path.join(options.swaggerRepo, options.readmeMd)}`;
    if (options.use) {
        cmd += ` --use=${options.use}`;
    }
    if (options.additionalArgs) {
        cmd += ` ${options.additionalArgs}`;
    }

    const cmds = new Set<string>();
    const readmeTypescriptMd = fs.readFileSync(path.join(path.dirname(path.join(options.swaggerRepo, options.readmeMd)), 'readme.typescript.md'), 'utf-8');
    const matches  = readmeTypescriptMd.match(/``` *ya?ml *\$\( *typescript *\)( *&& *!*\$\( *multi-client *\) *=* *'?"?[a-zA-z0-9-]*)?/gm)
    if (!matches) {
        throw new Error(`Cannot typescript block in readme.typescript.md`);
    }
    for (const match of matches) {
        if (match.includes('multi-client')) {
            if (/!\$\( *multi-client *\)/gm.exec(match)) {
                cmds.add(cmd);
            } else {
                const res = /\$\( *multi-client *\) *== *'?"?([a-zA-Z0-9-_]+)/gm.exec(match);
                if (!!res && res.length == 2) {
                    console.log(`--multi-client=${res[1]}`);
                    cmds.add(`${cmd} --multi-client=${res[1]}`)
                }
            }
        } else {
            cmds.add(cmd);
        }
    }

    for (const cmd of cmds) {
        logger.logGreen('Executing command:');
        logger.logGreen('------------------------------------------------------------');
        logger.logGreen(cmd);
        logger.logGreen('------------------------------------------------------------');
        try {
            execSync(cmd, {stdio: 'inherit'});
        } catch (e) {
            throw new Error(`An error occurred while generating codes for readme file: "${options.readmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
        }
    }

    const changedPackageDirectories: Set<string> = await getChangedPackageDirectory();

    for (const changedPackageDirectory of changedPackageDirectories) {
        const packagePath: string = path.join(options.sdkRepo, changedPackageDirectory);
        const swaggerSdkAutomationOutputPackageInfo: SwaggerSdkAutomationOutputPackageInfo = {
            "packageName": "",
            "path": [
                'rush.json',
                'common/config/rush/pnpm-lock.yaml',
                changedPackageDirectory
            ],
            "readmeMd": [
                options.readmeMd
            ],
            "changelog": {
                "content": "",
                "hasBreakingChange": false
            },
            "artifacts": [],
            "result": "succeeded"
        };

        try {
            const packageJson = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
            const packageName = packageJson.name;
            logger.logGreen(`Generate some other files for ${packageName} in ${packagePath}...`);
            await generateExtraFiles(packagePath, packageName, options.sdkRepo);

            // change configuration to skip build test, sample
            changeConfigOfTestAndSample(packagePath, ChangeModel.Change, SdkType.Rlc);

            logger.logGreen(`rush update...`);
            execSync('rush update', {stdio: 'inherit'});
            logger.logGreen(`rush build -t ${packageName}: Build generated codes, except test and sample, which may be written manually`);
            // To build generated codes except test and sample, we need to change tsconfig.json.
            execSync(`rush build -t ${packageName}`, {stdio: 'inherit'});
            logger.logGreen(`node common/scripts/install-run-rush.js pack --to ${packageName} --verbose`);
            execSync(`node common/scripts/install-run-rush.js pack --to ${packageName} --verbose`, {stdio: 'inherit'});
            logger.logGreen(`Generate changelog`);
            await generateChangelog(packagePath);

            swaggerSdkAutomationOutputPackageInfo.packageName = packageName;
            swaggerSdkAutomationOutputPackageInfo['version'] = packageJson.version;
            for (const file of fs.readdirSync(packagePath)) {
                if (file.startsWith('azure-rest') && file.endsWith('.tgz')) {
                    swaggerSdkAutomationOutputPackageInfo.artifacts.push(path.join(changedPackageDirectory, file));
                }
            }
        } catch (e) {
            logger.logError('Error:');
            logger.logError(`An error occurred while run build for readme file: "${options.readmeMd}":\nErr: ${e}\nStderr: "${e.stderr}"\nStdout: "${e.stdout}"\nErrorStack: "${e.stack}"`);
            swaggerSdkAutomationOutputPackageInfo.result = 'failed';
        } finally {
            if (options.outputJson) {
                options.outputJson.packages.push(swaggerSdkAutomationOutputPackageInfo);
            }
            changeConfigOfTestAndSample(packagePath, ChangeModel.Revert, SdkType.Rlc);
        }
    }

}
