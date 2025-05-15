import { isRushRepo } from "../../common/rushUtils.js";
import { modifyOrGenerateCiYml } from "../../utils/changeCiYaml.js";
import { changeRushJson } from "../../utils/changeRushJson.js";
import { getRelativePackagePath } from "./utils.js";

export async function generateExtraFiles(packagePath: string, packageName: string, sdkRepo: string) {
    await modifyOrGenerateCiYml(sdkRepo, packagePath, packageName, false);
    if(isRushRepo(sdkRepo)){
        await changeRushJson(sdkRepo, packageName, getRelativePackagePath(packagePath), 'client');
    }
}
