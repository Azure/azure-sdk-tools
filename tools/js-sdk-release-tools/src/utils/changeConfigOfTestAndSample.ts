import fs from "fs";
import path from "path";

let oriTsConfig;
let oriPackageJson;
let oriApiExtractor;

export enum ChangeModel {
    Change,
    Revert
}

export enum SdkType {
    Hlc,
    Rlc
}

export function changeConfigOfTestAndSample(packagePath: string, mode: ChangeModel, sdkType: SdkType) {
    let tsConfig;
    let packageJson;
    let apiExtractor;
    if (mode === ChangeModel.Change) {
        const tsConfigFile = fs.readFileSync(path.join(packagePath, 'tsconfig.json'), {encoding: 'utf-8'});
        const packageJsonFile = fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'});
        const apiExtractorFile = fs.readFileSync(path.join(packagePath, 'api-extractor.json'), {encoding: 'utf-8'})

        oriTsConfig = JSON.parse(tsConfigFile);
        oriPackageJson = JSON.parse(packageJsonFile);
        oriApiExtractor = JSON.parse(apiExtractorFile);

        tsConfig = JSON.parse(tsConfigFile);
        packageJson = JSON.parse(packageJsonFile);
        apiExtractor = JSON.parse(apiExtractorFile);

        tsConfig['include'] = ["./src/**/*.ts"];
        packageJson['module'] = "./dist-esm/index.js";
        if (sdkType === SdkType.Hlc) {
            apiExtractor['mainEntryPointFilePath'] = "./dist-esm/index.d.ts";
        } else {
            apiExtractor['mainEntryPointFilePath'] = "./types/index.d.ts";
        }

    }  else if (mode === ChangeModel.Revert) {
        tsConfig = oriTsConfig;
        packageJson = oriPackageJson;
        apiExtractor = oriApiExtractor;
    }

    fs.writeFileSync(path.join(packagePath, 'tsconfig.json'), JSON.stringify(tsConfig, undefined, '  '), {encoding: 'utf-8'});
    fs.writeFileSync(path.join(packagePath, 'package.json'), JSON.stringify(packageJson, undefined, '  '), {encoding: 'utf-8'});
    fs.writeFileSync(path.join(packagePath, 'api-extractor.json'), JSON.stringify(apiExtractor, undefined, '  '), {encoding: 'utf-8'});
}
