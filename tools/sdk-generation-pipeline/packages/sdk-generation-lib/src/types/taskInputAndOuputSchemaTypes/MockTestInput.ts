import { requireJsonc } from '../../utils/requireJsonc';
import { getTypeTransformer } from '../../utils/validator';

export const mockTestInputSchema = requireJsonc(__dirname + '/MockTestInputSchema.json');

export type MockTestInput = {
    packageFolder: string;
    mockServerHost: string;
};

export const getMockTestInput = getTypeTransformer<MockTestInput>(mockTestInputSchema, 'MockTestInput');
