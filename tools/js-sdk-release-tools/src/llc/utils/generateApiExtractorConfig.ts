import * as fs from "fs";
import * as path from "path";
import {getPackageFolderName} from "./utils";

export function generateApiExtractorConfig(packagePath, packageName) {
    const content = {
        "$schema": "https://developer.microsoft.com/json-schemas/api-extractor/v7/api-extractor.schema.json",
        "mainEntryPointFilePath": "types/src/index.d.ts",
        "docModel": {
            "enabled": true
        },
        "apiReport": {
            "enabled": true,
            "reportFolder": "./review"
        },
        "dtsRollup": {
            "enabled": true,
            "untrimmedFilePath": "",
            "publicTrimmedFilePath": `./types/${getPackageFolderName(packageName)}.d.ts`
        },
        "messages": {
            "tsdocMessageReporting": {
                "default": {
                    "logLevel": "none"
                }
            },
            "extractorMessageReporting": {
                "ae-missing-release-tag": {
                    "logLevel": "none"
                },
                "ae-unresolved-link": {
                    "logLevel": "none"
                }
            }
        }
    };
    fs.writeFileSync(path.join(packagePath, 'api-extractor.json'), JSON.stringify(content, undefined, '  '), {encoding: 'utf-8'});
}
