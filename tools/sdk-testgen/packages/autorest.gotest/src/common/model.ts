import { ExampleModel, MockTestDefinitionModel, TestDefinitionModel } from '@autorest/testmodeler/dist/src/core/model';

interface GoFileData {
    packageName: string;
    packagePath?: string;
    imports: string;
}

export class GoMockTestDefinitionModel extends MockTestDefinitionModel implements GoFileData {
    packageName: string;
    imports: string;
}

export type GoTestDefinition = TestDefinitionModel & GoFileData;

export class GoExampleModel extends ExampleModel {
    opName: string;
    isLRO: boolean;
    isPageable: boolean;
    isMultiRespOperation: boolean;
    methodParametersOutput: string;
    methodParametersPlaceholderOutput: string;
    clientParametersOutput: string;
    clientParametersPlaceholderOutput: string;
    returnInfo: string[];
    checkResponse: boolean;
    responseOutput: string;
    responseType: string;
    responseTypePointer: boolean;
    pollerType: string;
    pageableType: string;
    pageableItemName: string;
}
