import { requireJsonc } from '../../utils/requireJsonc';
import { getTypeTransformer } from '../../utils/validator';

export const testOutputSchema = requireJsonc(__dirname + '/TestOutputSchema.json');

export type TestOutput = {
    total: number;
    success: number;
    fail: number;
    apiCoverage: number;
    codeCoverage: number;
};

export const getTestOutput = getTypeTransformer<TestOutput>(testOutputSchema, 'TestOutput');
