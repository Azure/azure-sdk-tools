import { ApiVersionType, SDKType } from "../../common/types";
import { getSDKType } from "../../common/utils";
import { logger } from "../../utils/logger";

import * as fs from "fs";
import * as path from "path";

export function updateUserAgent(packageFolderPath: string, packageVersion: string) {
    const packageJsonData: any = JSON.parse(fs.readFileSync(path.join(packageFolderPath, 'package.json'), 'utf8'));
    const packageName = packageJsonData.name.replace("@azure/", "");
    const sdkType = getSDKType(packageFolderPath);
    let files: string[];
    switch (sdkType) {
        case SDKType.HighLevelClient:
            // Update version in src for HLC
            files = fs.readdirSync(path.join(packageFolderPath, 'src'));
            files.forEach(file => {
                if (file.endsWith('.ts')) {
                    const data: string = fs.readFileSync(path.join(packageFolderPath, 'src', file), 'utf8');
                    const result = data.replace(/const packageDetails = `azsdk-js-[0-9a-z-]+\/[0-9.a-z-]+`;/g, 'const packageDetails = `azsdk-js-' + packageName + '/' + packageVersion + '`;');
                    fs.writeFileSync(path.join(packageFolderPath, 'src', file), result, 'utf8');
                }
            })
            break;
        case SDKType.ModularClient:
            // Update version in src for Modular
            files = fs.readdirSync(path.join(packageFolderPath, 'src', "api"));
            files.forEach(file => {
                if (file.endsWith('Context.ts')) {
                    const data: string = fs.readFileSync(path.join(packageFolderPath, 'src', "api", file), 'utf8');
                    const result = data.replace(/const userAgentInfo = `azsdk-js-[0-9a-z-]+\/[0-9.a-z-]+`;/g, 'const userAgentInfo = `azsdk-js-' + packageName + '/' + packageVersion + '`;');
                    fs.writeFileSync(path.join(packageFolderPath, 'src', 'api', file), result, 'utf8');
                }
            })
            break;
        case SDKType.RestLevelClient:
            // Update version in src for RLC
            files = fs.readdirSync(path.join(packageFolderPath, 'src'));
            files.forEach(file => {
                if (file.endsWith('.ts')) {
                    const data: string = fs.readFileSync(path.join(packageFolderPath, 'src', file), 'utf8');
                    const result = data.replace(/const userAgentInfo = `azsdk-js-[0-9a-z-]+\/[0-9.a-z-]+`;/g, 'const userAgentInfo = `azsdk-js-' + packageName + '/' + packageVersion + '`;');
                    fs.writeFileSync(path.join(packageFolderPath, 'src', file), result, 'utf8');
                }
            })
            break;
        default:
            logger.warn(`Unsupported SDK type ${sdkType} to update user agent`);
            return ApiVersionType.None;
    }
}