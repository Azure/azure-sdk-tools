import { requireJsonc } from '../../utils/requireJsonc';
import { getTypeTransformer } from '../../utils/validator';

export const liveTestInputSchema = requireJsonc(__dirname + '/LiveTestInputSchema.json');

export type LiveTestInput = {
    packageFolder: string;
};

export const getLiveTestInput = getTypeTransformer<LiveTestInput>(liveTestInputSchema, 'MockTestInput');
