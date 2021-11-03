import { MockTool } from '../../tools';
import { TestCodeModel, TestCodeModeler } from '@autorest/testmodeler/dist/src/core/model';
import { TestConfig } from '@autorest/testmodeler/dist/src/common/testConfig';
import { TestGenerator } from '../../../src/generator/testGenerator';

class FakeGenerator extends TestGenerator {
    public getMockTestFilename() {
        return `mock_test.go`;
    }
    public genRenderDataForMock() {
        // Fake genRenderData
    }
    public genRenderDataForDefinition(_) {
        // Fake genRenderDataForDefinition
    }
    public getScenarioTestFilename(_): string {
        return 'Fake getScenarioTestFilename';
    }
}

describe('TestGenerator functions', () => {
    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('generateMockTest', async () => {
        const codeModel = MockTool.createCodeModel();
        const testCodeModel = TestCodeModeler.createInstance(codeModel as TestCodeModel, {
            testmodeler: {
                'split-parents-value': true,
            },
        });
        testCodeModel.genMockTests();

        const mockWriteFile = jest.fn();
        const generator = new FakeGenerator(
            {
                // eslint-disable-next-line @typescript-eslint/naming-convention
                WriteFile: mockWriteFile,
            } as any,
            testCodeModel.codeModel,
            new TestConfig({}),
        );
        generator.genRenderData = jest.fn();
        const spyRender = jest.spyOn(FakeGenerator.prototype, 'render').mockImplementation();
        generator.genRenderData();
        await generator.generateMockTest('fake.njk');

        const generationTimes = 1;
        expect(generator.genRenderData).toBeCalledTimes(generationTimes);
        expect(spyRender).toBeCalledTimes(generationTimes);
        expect(mockWriteFile).toBeCalledTimes(generationTimes);
    });

    it('parseOavVariable', async () => {
        const generator = new FakeGenerator(undefined, undefined, new TestConfig({}));

        expect(generator.parseOavVariable('$(abc)', { abc: 'fake' })).toMatchSnapshot();
        expect(generator.parseOavVariable('$(abc)', { ac: 'fake' })).toMatchSnapshot();
        expect(generator.parseOavVariable('$(abc) with tail txt', { abc: 'fake' })).toMatchSnapshot();
        expect(generator.parseOavVariable('head texted $(abc)', { abc: 'fake' })).toMatchSnapshot();
        expect(
            generator.parseOavVariable('multiple $(abc) variables $(dbe) ... $(abc).', {
                abc: 'fake',
                dbe: 'fake2',
            }),
        ).toMatchSnapshot();
    });
});
