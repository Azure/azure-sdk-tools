import * as child_process from "child_process";


export function getFileListInPackageFolder(packageFolder: string) {
    child_process.execSync('git add .',{ encoding: "utf8", cwd: packageFolder });
    const files = child_process.execSync('git ls-files',{ encoding: "utf8", cwd: packageFolder }).trim().split('\n');

    return files;
}
