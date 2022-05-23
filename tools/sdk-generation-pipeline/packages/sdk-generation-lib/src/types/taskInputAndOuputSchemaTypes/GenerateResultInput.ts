import { requireJsonc } from '../../utils/requireJsonc';
import { getTypeTransformer } from '../../utils/validator';
import { TaskResultStatus } from '../taskResult';

export const generateResultInputSchema = requireJsonc(__dirname + '/GenerateResultInputSchema.json');

export type GenerateResultInput = {
    pipelineBuildId: string;
    logfile: string;
    logFilterStr?: string;
    taskName: string;
    exeResult?: TaskResultStatus;
    taskOutput?: string;
};

export const getGenerateResultInput = getTypeTransformer<GenerateResultInput>(
    generateResultInputSchema,
    'GenerateResultInput'
);
