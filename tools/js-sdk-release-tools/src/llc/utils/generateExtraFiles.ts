import {generateTsConfig} from "./generateTsConfig";
import {generatePackageJson} from "./generatePackageJson";
import {generateRollupConfig} from "./generateRollupConfig";
import {generateApiExtractorConfig} from "./generateApiExtractorConfig";
import {generateLinterConfig} from "./generateLinterConfig";
import {generateLicense} from "./generateLicense";
import {generateReadmeMd} from "./generateReadmeMd";
import {generateTest} from "./generateTest";
import {generateKarmaConfig} from "./generateKarmaConfig";
import {generateSample} from "./generateSample";
import {modifyOrGenerateCiYml} from "../../utils/changeCiYaml";
import {changeRushJson} from "../../utils/changeRushJson";
import {getRelativePackagePath} from "./utils";

export async function generateExtraFiles(packagePath: string, packageName: string, sdkRepo: string) {
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
    await modifyOrGenerateCiYml(sdkRepo, packagePath, packageName, false);
    await changeRushJson(sdkRepo, packageName, getRelativePackagePath(packagePath), 'client');
}
