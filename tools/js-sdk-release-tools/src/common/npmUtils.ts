import { load } from '@npmcli/package-json';
import { NpmPackageInfo } from './types';
import * as fetch from 'npm-registry-fetch';

export async function getNpmPackageInfo(packageDirectory): Promise<NpmPackageInfo> {
    const packageJson = await load(packageDirectory);
    if (!packageJson.content.name) {
        throw new Error(`package.json doesn't contains name property`);
    }
    if (!packageJson.content.version) {
        throw new Error(`package.json doesn't contains version property`);
    }
    const name = packageJson.content.name;
    const version = packageJson.content.version;
    return { name, version };
}

export function getNpmPackageName(info: NpmPackageInfo) {
    return info.name.replace('@azure/', 'azure-');
}

export function getNpmPackageSafeName(info: NpmPackageInfo) {
    const name = getNpmPackageName(info);
    const safeName = name.replace(/-/g, '');
    return safeName;
}

export function getArtifactName(info: NpmPackageInfo) {
    const name = getNpmPackageName(info);
    const version = info.version;
    return `${name}-${version}.tgz`;
}

export async function tryGetNpmView(packageName: string): Promise<{[id: string]: any} | undefined> {
    try {
        return await fetch.json(`/${packageName}`);
    } catch (err) {
        return undefined;
    }
}