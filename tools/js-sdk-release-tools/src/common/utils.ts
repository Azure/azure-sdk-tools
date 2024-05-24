import shell from 'shelljs';
import path from 'path';
import { SDKType } from './types'

export function getClassicClientParametersPath(packageRoot: string): string {
    return path.join(packageRoot, 'src', 'models', 'parameters.ts');
}

export function getSDKType(packageRoot: string): SDKType {
    const paraPath = getClassicClientParametersPath(packageRoot);
    const exist = shell.test('-e', paraPath);
    const type = exist ? SDKType.HLC : SDKType.MLC;
    console.log(`SDK type: ${type} detected`);
    return type;
}