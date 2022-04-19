import * as fs from "fs";
import * as path from "path";
import {NPMScope} from "@ts-common/azure-js-dev-tools";
import {logger} from "../../utils/logger";
import {getLatestStableVersion} from "../../utils/version";
import {extractExportAndGenerateChangelog} from "../../changelog/extractMetaData";

const shell = require('shelljs');
const todayDate = new Date();
const dd = String(todayDate.getDate()).padStart(2, '0');
const mm = String(todayDate.getMonth() + 1).padStart(2, '0'); //January is 0!
const yyyy = todayDate.getFullYear();
const date = yyyy + '-' + mm + '-' + dd;

function generateChangelogForFirstRelease(packagePath, version) {
    const content = `## ${version} (${date})

  - Initial Release
`;
    fs.writeFileSync(path.join(packagePath, 'CHANGELOG.md'), content, 'utf8');
}

function appendChangelog(packagePath, version, changelog) {
    const originalChangeLogContent = fs.readFileSync(path.join(packagePath, 'changelog-temp', 'package', 'CHANGELOG.md'), {encoding: 'utf-8'});
    const modifiedChangelogContent = `## ${version} (${date})
    
${changelog.displayChangeLog()}
    
${originalChangeLogContent}`;
    fs.writeFileSync(path.join(packagePath, 'CHANGELOG.md'), modifiedChangelogContent, {encoding: 'utf-8'});
}

export async function generateChangelog(packagePath) {
    const jsSdkRepoPath = String(shell.pwd());
    const packageJson = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
    const packageName = packageJson.name;
    const version = packageJson.version;
    const npm = new NPMScope({executionFolderPath: packagePath});
    const npmViewResult = await npm.view({packageName});
    if (npmViewResult.exitCode !== 0) {
        logger.logGreen(`${packageName} is first release, generating changelog`);
        generateChangelogForFirstRelease(packagePath, version);
        logger.logGreen(`Generate changelog successfully`);
    } else {
        const stableVersion = getLatestStableVersion(npmViewResult);
        if (!stableVersion) {
            logger.logError(`Invalid latest version ${stableVersion}`);
            process.exit(1);
        }
        logger.log(`Package ${packageName} released is released before`);
        logger.log('Generating changelog by comparing api.md...');
        try {
            await shell.mkdir(path.join(packagePath, 'changelog-temp'));
            await shell.cd(path.join(packagePath, 'changelog-temp'));
            await shell.exec(`npm pack ${packageName}@${stableVersion}`);
            await shell.exec('tar -xzf *.tgz');
            await shell.cd(packagePath);
            const tempReviewFolder = path.join(packagePath, 'changelog-temp', 'package', 'review');
            if (!fs.existsSync(tempReviewFolder)) {
                logger.logWarn("The latest package released in NPM doesn't contain review folder, so generate changelog same as first release");
                generateChangelogForFirstRelease(packagePath, version);
            } else {
                let apiMdFileNPM = path.join(tempReviewFolder, fs.readdirSync(tempReviewFolder)[0]);
                let apiMdFileLocal = path.join(packagePath, 'review', fs.readdirSync(path.join(packagePath, 'review'))[0]);
                const changelog = await extractExportAndGenerateChangelog(apiMdFileNPM, apiMdFileLocal);
                if (!changelog.hasBreakingChange && !changelog.hasFeature) {
                    logger.logError('Cannot generate changelog because the codes of local and npm may be the same.');
                } else {
                    appendChangelog(packagePath, version, changelog);
                    logger.log('Generate changelog successfully');
                }
            }

        } catch (e) {
          logger.logError(`Generate changelog failed: ${e.message}`);
        } finally {
            fs.rmSync(path.join(packagePath, 'changelog-temp'), { recursive: true, force: true });
            await shell.cd(jsSdkRepoPath);
        }
    }
}
