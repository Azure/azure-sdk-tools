import * as _ from 'lodash';
import { Config } from './constant';
export class TestConfig {
    constructor(public readonly config: Record<string, any>) {}

    public getValue(path: string, d: any = undefined) {
        return _.get(this.config, path, d);
    }

    public getSwaggerFolder(): string {
        const parentsOptions = this.getValue(Config.parents);
        for (const k of Object.keys(parentsOptions || {})) {
            const v: string = parentsOptions[k];
            if (k.endsWith('.json') && typeof v === 'string' && v.startsWith('file:///')) {
                if (process.platform.toLowerCase().startsWith('win')) {
                    return v.slice('file:///'.length);
                }
                return v.slice('file:///'.length - 1);
            }
        }
        return undefined;
    }

    public isDisabledExample(exampleName: string): boolean {
        const disabledExamples = this.getValue(Config.disabledExamples);
        if (disabledExamples !== undefined && disabledExamples !== null && Array.isArray(disabledExamples)) {
            return (disabledExamples as string[]).indexOf(exampleName) >= 0;
        }
        return false;
    }
}
