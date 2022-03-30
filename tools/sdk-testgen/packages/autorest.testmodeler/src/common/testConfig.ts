import * as _ from 'lodash';
import * as path from 'path';
import { Config } from './constant';
export class TestConfig {
    constructor(public readonly config: Record<string, any>, public readonly defaultValues: unknown) {}

    public getValue(path: string, d: any = undefined) {
        if (d === undefined) {
            return _.get(this.config, path, this.defaultValues[path]);
        } else {
            return _.get(this.config, path, d);
        }
    }

    public getSwaggerFolder(): string {
        const parentsOptions = this.getValue(Config.parents);
        for (const k of Object.keys(parentsOptions || {})) {
            const v: string = parentsOptions[k];
            if (k.endsWith('.json') && typeof v === 'string') {
                if (v.startsWith('file:///')) {
                    if (process.platform.toLowerCase().startsWith('win')) {
                        return v.slice('file:///'.length);
                    }
                    return v.slice('file:///'.length - 1);
                } else if (v.startsWith('https://') || v.startsWith('http://')) {
                    return v;
                }
            }
        }
        return undefined;
    }

    public getInputFileFolders(): string[] {
        const inputFiles: string[] = this.getValue(Config.inputFile, []);
        const allFolders = new Set<string>();
        for (const file of inputFiles) {
            allFolders.add(path.dirname(file));
        }
        return [...allFolders];
    }

    public isDisabledExample(exampleName: string): boolean {
        const disabledExamples = this.getValue(Config.disabledExamples);
        if (disabledExamples !== undefined && disabledExamples !== null && Array.isArray(disabledExamples)) {
            return (disabledExamples as string[]).indexOf(exampleName) >= 0;
        }
        return false;
    }
}
