import shell from 'shelljs';
import path from 'path';
import fs from 'fs';

import { SDKType } from './types'
import {logger} from "../utils/logger";

export function getClassicClientParametersPath(packageRoot: string): string {
    return path.join(packageRoot, 'src', 'models', 'parameters.ts');
}

export function getSDKType(packageRoot: string): SDKType {
    const paraPath = getClassicClientParametersPath(packageRoot);
    const exist = shell.test('-e', paraPath);
    const type = exist ? SDKType.HLC : SDKType.MLC;
    logger.logInfo(`SDK type: ${type} detected in ${packageRoot}`);
    return type;
}

export function getApiReviewPath(packageRoot: string): string {
    const sdkType = getSDKType(packageRoot);
    const reviewDir = path.join(packageRoot, 'review');
    switch (sdkType) {
        case SDKType.MLC:
            const fileNames = fs.readdirSync(reviewDir).sort();
            if (fileNames.length === 0) {
                logger.logError(`Expects 1 API report, but find nothing.`);
                process.exit(1);
            }
            // TODO: use a more concrete rule to find xxx.api.md
            return path.join(packageRoot, 'review', fileNames[fileNames.length - 1]);
        case SDKType.HLC:
        case SDKType.RLC:
        default:
            // only one xxx.api.md
            return path.join(packageRoot, 'review', fs.readdirSync(reviewDir)[0]);
    }

}