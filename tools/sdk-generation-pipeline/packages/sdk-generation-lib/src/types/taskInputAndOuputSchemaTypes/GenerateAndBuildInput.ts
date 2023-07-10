import { requireJsonc } from '../../utils/requireJsonc';
import { getTypeTransformer } from '../../utils/validator';

export const generateAndBuildInputSchema = requireJsonc(__dirname + '/GenerateAndBuildInputSchema.json');

export type GenerateAndBuildInput = {
    specFolder: string;
    headSha: string;
    headRef: string;
    repoHttpsUrl: string;
    relatedReadmeMdFile?: string;
    relatedTypeSpecProjectFolder?: string;
    serviceType: string;
    autorestConfig: string;
    skipGeneration: boolean;
};

export const getGenerateAndBuildInput = getTypeTransformer<GenerateAndBuildInput>(
    generateAndBuildInputSchema,
    'GenerateAndBuildInput'
);
