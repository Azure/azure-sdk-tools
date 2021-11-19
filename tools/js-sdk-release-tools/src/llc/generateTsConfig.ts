import * as fs from "fs";
import * as path from "path";

export function generateTsConfig(packagePath, packageName) {
    const content = {
        extends: "../../../tsconfig.package",
        compilerOptions: {
            "outDir": "./dist-esm",
            "declarationDir": "./types",
            "paths": {
            }
        },
        include: ["src/**/*.ts", "test/**/*.ts", "samples-dev/**/*.ts"]
    };
    content.compilerOptions.paths[packageName] = ['./src/index'];
    fs.writeFileSync(path.join(packagePath, 'tsconfig.json'), JSON.stringify(content, undefined, '  '), {encoding: 'utf-8'});
}
