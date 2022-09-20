import { modifyOrGenerateCiYml } from "../../utils/changeCiYaml";
import { changeRushJson } from "../../utils/changeRushJson";
import { getRelativePackagePath } from "./utils";

export async function generateExtraFiles(packagePath: string, packageName: string, sdkRepo: string) {
    await modifyOrGenerateCiYml(sdkRepo, packagePath, packageName, false);
    await changeRushJson(sdkRepo, packageName, getRelativePackagePath(packagePath), 'client');
}
