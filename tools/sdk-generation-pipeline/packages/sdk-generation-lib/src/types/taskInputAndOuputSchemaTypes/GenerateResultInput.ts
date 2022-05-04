import { requireJsonc } from '../../utils/requireJsonc';
import { getTypeTransformer } from '../../utils/validator';

export const generateResultInputSchema = requireJsonc(__dirname + '/GenerateResultInputSchema.json');

export type GenerateResultInput = {
    pipelineBuildId: string;
    logfile: string;
    logFilterStr?: string;
    taskName: string;
    exeResult?: string;
    taskOutput?: string;
};

export const getGenerateResultInput = getTypeTransformer<GenerateResultInput>(
    generateResultInputSchema,
    'GenerateResultInput'
);
