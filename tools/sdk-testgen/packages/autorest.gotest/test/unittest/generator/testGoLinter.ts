import * as path from 'path';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { processRequest } from '../../../src/generator/goLinter';

describe('processRequest of go-linter', () => {
    beforeEach(() => {
        Helper.execSync = jest.fn().mockReturnThis();
    });
    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('call go-fmt on go outputFiles only', async () => {
        const outputFolder = 'mocked-folder';
        const files = ['mocked-file1.go', 'mocked-file2.yaml', 'mocked-file3.go'];
        await processRequest({
            // eslint-disable-next-line @typescript-eslint/naming-convention
            GetValue: async (_) => {
                return Promise.resolve({
                    'output-folder': outputFolder,
                });
            },
            // eslint-disable-next-line @typescript-eslint/naming-convention
            listInputs: async (_) => {
                return Promise.resolve(files);
            },
        } as any);

        for (const file of files) {
            if (file.endsWith('.go')) {
                expect(Helper.execSync).toHaveBeenCalledWith(`go fmt ${path.join(outputFolder, file)}`);
            } else {
                expect(Helper.execSync).not.toHaveBeenCalledWith(`go fmt ${path.join(outputFolder, file)}`);
            }
        }
    });
});
