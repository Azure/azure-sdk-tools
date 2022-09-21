import { ValidateFunction } from 'ajv';

const ajvInstance = require('ajv');

const ajv = new ajvInstance({
    coerceTypes: true,
    messages: true,
    verbose: true,
    useDefaults: true
});

export const getTypeTransformer = <T>(schema: object, name: string) => {
    let validator: ValidateFunction | undefined;
    return (obj: unknown) => {
        if (validator === undefined) {
            validator = ajv.compile(schema);
        }
        if (!validator(obj)) {
            const error = validator.errors![0];
            throw new Error(`Invalid ${name}: ${error.dataPath} ${error.message}`);
        }

        return obj as T;
    };
};
