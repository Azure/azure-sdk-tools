import { ExampleParameter, ExampleValue, TestCodeModel, TestCodeModeler } from '../../../src/core/model';
import { Helper } from '../../../src/util/helper';
import { MockTool } from '../../tools';
import { SchemaType } from '@autorest/codemodel';
import { serialize } from '@azure-tools/codegen';

describe('ExampleValue.createInstance(...)', () => {
    const codeModel = MockTool.createCodeModel();
    beforeAll(() => {
        TestCodeModeler.createInstance(codeModel as TestCodeModel, {
            testmodeler: {
                'split-parents-value': true,
            },
        });
    });
    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('create with primitive schema', async () => {
        const spyCreateInstance = jest.spyOn(ExampleValue, 'createInstance');
        const rawValue = [1, 2, 3];
        const instance = ExampleValue.createInstance(rawValue, new Set(), MockTool.createSchema(SchemaType.Any), MockTool.createLanguages());
        expect(spyCreateInstance).toHaveBeenCalledTimes(1);
        expect(instance).toMatchSnapshot();
    });

    it('recursively call on array element schema', async () => {
        const spyCreateInstance = jest.spyOn(ExampleValue, 'createInstance');
        const rawValue = [1, 2, 3];
        const instance = ExampleValue.createInstance(rawValue, new Set(), MockTool.createArraySchema(MockTool.createSchema(SchemaType.Integer)), MockTool.createLanguages());
        expect(spyCreateInstance).toHaveBeenCalledTimes(rawValue.length + 1);
        expect(instance).toMatchSnapshot();
    });

    it('recursively call on object element schema', async () => {
        const spyCreateInstance = jest.spyOn(ExampleValue, 'createInstance');
        const rawValue = {
            abc: 1,
            bcd: 2,
        };
        const integerSchema = MockTool.createSchema(SchemaType.Integer);
        const instance = ExampleValue.createInstance(
            rawValue,
            new Set(),
            MockTool.createSchema(SchemaType.Object, {
                properties: [MockTool.createProperty('abc', integerSchema), MockTool.createProperty('bcd', integerSchema)],
            }),
            MockTool.createLanguages(),
        );
        expect(spyCreateInstance).toHaveBeenCalledTimes(Object.keys(rawValue).length + 1);
        expect(instance).toMatchSnapshot();
    });

    it('recursively call on dictionary element schema', async () => {
        const spyCreateInstance = jest.spyOn(ExampleValue, 'createInstance');
        const rawValue = {
            abc: 1,
            bcd: 2,
        };
        const integerSchema = MockTool.createSchema(SchemaType.Integer);
        const instance = ExampleValue.createInstance(
            rawValue,
            new Set(),
            MockTool.createSchema(SchemaType.Dictionary, {
                elementType: integerSchema,
            }),
            MockTool.createLanguages(),
        );
        expect(spyCreateInstance).toHaveBeenCalledTimes(Object.keys(rawValue).length + 1);
        expect(instance).toMatchSnapshot();
    });
});

describe('ExampleParameter constructor(...)', () => {
    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('create normal ExampleParameter', async () => {
        const integerSchema = MockTool.createSchema(SchemaType.Integer);
        const spyCreateInstance = jest.spyOn(ExampleValue, 'createInstance');
        const rawValue = [1, 2, 3];
        const instance = new ExampleParameter(MockTool.createParameter(integerSchema), rawValue);
        expect(spyCreateInstance).toHaveBeenCalled();
        expect(instance).toMatchSnapshot();
    });
});

describe('TestCodeModel functions', () => {
    const codeModel = MockTool.createCodeModel();

    afterEach(() => {
        jest.restoreAllMocks();
    });

    describe('genMockTests', () => {
        it('genMockTests', async () => {
            const testCodeModel = TestCodeModeler.createInstance(codeModel as TestCodeModel, {
                testmodeler: {
                    'split-parents-value': true,
                },
            });
            testCodeModel.genMockTests();
            expect(serialize(testCodeModel.codeModel.testModel.mockTest)).toMatchSnapshot();
        });
    });

    describe('genMockTests with some example disabled', () => {
        it('genMockTests', async () => {
            const testCodeModel = TestCodeModeler.createInstance(codeModel as TestCodeModel, {
                testmodeler: {
                    mock: {
                        ['disabled-examples']: ['Extensions_Get', 'Extensions_Delete'],
                    },
                    'split-parents-value': true,
                },
            });
            testCodeModel.genMockTests();
            expect(serialize(testCodeModel.codeModel.testModel.mockTest)).toMatchSnapshot();
        });
    });
});

describe('tools for core', () => {
    afterEach(() => {
        jest.restoreAllMocks();
    });

    it('getFlattenedNames()', async () => {
        const flattenedBody = {
            originalParameter: {
                language: {
                    default: {
                        name: 'AA',
                        description: '',
                    },
                },
            },
            language: {
                default: {
                    name: 'BB',
                    serializedName: 'EE',
                },
            },
            targetProperty: {
                noFlattenedNames: [],
            },
        };
        expect(Helper.getFlattenedNames(flattenedBody as any)).toMatchSnapshot();

        const flattenedProperty = {
            originalParameter: {
                language: {
                    default: {
                        name: 'AA',
                        description: '',
                    },
                },
            },
            language: {
                default: {
                    name: 'BB',
                    serializedName: 'EE',
                },
            },
            targetProperty: {
                flattenedNames: ['CC', 'DD'],
            },
        };
        expect(Helper.getFlattenedNames(flattenedProperty as any)).toMatchSnapshot();

        const normalParameter = {
            language: {
                default: {
                    name: 'BB',
                    serializedName: 'EE',
                },
            },
        };
        expect(Helper.getFlattenedNames(normalParameter as any)).toMatchSnapshot();
    });
});
