import * as fs from "fs";
import * as path from "path";

export function generateLinterConfig(packagePath) {
    const content = {
        "plugins": ["@azure/azure-sdk"],
        "extends": ["plugin:@azure/azure-sdk/azure-sdk-base"],
        "rules": {
            "@azure/azure-sdk/ts-modules-only-named": "warn",
            "@azure/azure-sdk/ts-apiextractor-json-types": "warn",
            "@azure/azure-sdk/ts-package-json-types": "warn",
            "@azure/azure-sdk/ts-package-json-engine-is-present": "warn",
            "tsdoc/syntax": "warn"
        }
    };
    fs.writeFileSync(path.join(packagePath, '.eslintrc.json'), JSON.stringify(content, undefined, '  '), {encoding: 'utf-8'});
}
