import { Helper } from '../../../src/util/helper';
import { TestCodeModeler } from '../../../src/core/model';
import { processRequest } from '../../../src/core/testModeler';

describe('TestModeler functions', () => {
    beforeEach(() => {
        Helper.outputToModelerfour = jest.fn().mockResolvedValue(undefined);
        Helper.dump = jest.fn().mockResolvedValue(undefined);
        Helper.addCodeModelDump = jest.fn().mockResolvedValue(undefined);
    });
    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('processRequest with export-codemodel', async () => {
        TestCodeModeler.getSessionFromHost = jest.fn().mockResolvedValue({
            getValue: jest.fn().mockResolvedValue({
                testmodeler: {
                    'export-codemodel': true,
                    'export-explicit-type': true,
                },
            }),
        });
        const spyGenMockTests = jest.spyOn(TestCodeModeler.prototype, 'genMockTests').mockImplementation();
        await processRequest(undefined);

        expect(spyGenMockTests).toHaveBeenCalledTimes(1);
        expect(Helper.outputToModelerfour).toHaveBeenCalledTimes(1);
        expect(Helper.addCodeModelDump).toHaveBeenCalledTimes(3);
        expect(Helper.dump).toHaveBeenCalledTimes(1);
    });

    it('processRequest without export-codemode', async () => {
        TestCodeModeler.getSessionFromHost = jest.fn().mockResolvedValue({
            getValue: jest.fn().mockResolvedValue({
                testmodeler: {
                    'export-codemodel': false,
                },
            }),
        });
        const spyGenMockTests = jest.spyOn(TestCodeModeler.prototype, 'genMockTests').mockImplementation();
        await processRequest(undefined);

        expect(spyGenMockTests).toHaveBeenCalledTimes(1);
        expect(Helper.outputToModelerfour).toHaveBeenCalledTimes(1);
        expect(Helper.addCodeModelDump).not.toHaveBeenCalled();
        expect(Helper.dump).toHaveBeenCalledTimes(1);
    });
});
