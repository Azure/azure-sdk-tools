import { requireJsonc } from '../../utils/requireJsonc';
import { getTypeTransformer } from '../../utils/validator';

export const generateAndBuildOutputSchema = requireJsonc(__dirname + '/GenerateAndBuildOutputSchema.json');

export type PackageResult = {
    packageName: string;
    result: string;
    path: string[];
    packageFolder: string;
    changelog?: {
        content: string;
        hasBreakingChange?: boolean;
        breakingChangeItems?: string[]
    }
    artifacts?: string[]
}

export type GenerateAndBuildOutput = {
    packages: PackageResult[];
};

export const getGenerateAndBuildOutput = getTypeTransformer<GenerateAndBuildOutput>(
    generateAndBuildOutputSchema,
    'GenerateAndBuildOutput'
);
