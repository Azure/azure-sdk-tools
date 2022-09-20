import { requireJsonc } from '../../utils/requireJsonc';
import { getTypeTransformer } from '../../utils/validator';

export const initOutputSchema = requireJsonc(__dirname + '/InitOutputSchema.json');

export declare type StringMap<TValue> = {
    [key: string]: TValue;
};
export type InitOutput = {
    envs: StringMap<string | boolean | number>;
};

export const initOutput = getTypeTransformer<InitOutput>(
    initOutputSchema,
    'InitOutput'
);
