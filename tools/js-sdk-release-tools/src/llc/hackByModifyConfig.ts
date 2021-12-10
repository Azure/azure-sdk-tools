import * as fs from "fs";
import * as path from "path";

export enum ModifyModel {
    Change,
    Revert
}

export function hackByModifyConfig(packagePath: string, mode: ModifyModel) {
    const tsConfig = JSON.parse(fs.readFileSync(path.join(packagePath, 'tsconfig.json'), {encoding: 'utf-8'}));
    const packageJson = JSON.parse(fs.readFileSync(path.join(packagePath, 'package.json'), {encoding: 'utf-8'}));
    const apiExtractor = JSON.parse(fs.readFileSync(path.join(packagePath, 'api-extractor.json'), {encoding: 'utf-8'}));
    if (mode === ModifyModel.Change) {
        tsConfig['include'] = ["src/**/*.ts"];
        packageJson['module'] = "./dist-esm/index.js";
        apiExtractor['mainEntryPointFilePath'] = "types/index.d.ts";
    } else if (mode === ModifyModel.Revert) {
        tsConfig['include'] = ["src/**/*.ts", "test/**/*.ts", "samples-dev/**/*.ts"];
        packageJson['module'] = "./dist-esm/src/index.js";
        apiExtractor['mainEntryPointFilePath'] = "types/src/index.d.ts";
    }
    fs.writeFileSync(path.join(packagePath, 'tsconfig.json'), JSON.stringify(tsConfig, undefined, '  '), {encoding: 'utf-8'});
    fs.writeFileSync(path.join(packagePath, 'package.json'), JSON.stringify(packageJson, undefined, '  '), {encoding: 'utf-8'});
    fs.writeFileSync(path.join(packagePath, 'api-extractor.json'), JSON.stringify(apiExtractor, undefined, '  '), {encoding: 'utf-8'});
}
