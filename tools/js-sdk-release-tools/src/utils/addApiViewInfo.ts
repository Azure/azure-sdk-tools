import fs from "fs";
import path from "path";

export function addApiViewInfo(outputPackageInfo: any, packagePath: string, changedPackageDirectory: string) {
    if (fs.existsSync(path.join(packagePath, 'temp'))) {
        for (const file of fs.readdirSync(path.join(packagePath, 'temp'))) {
            if (file.endsWith('.api.json')) {
                outputPackageInfo['apiViewArtifact']= path.join(changedPackageDirectory, 'temp', file);
                outputPackageInfo['language']= 'JavaScript';
            }
        }
    }
}