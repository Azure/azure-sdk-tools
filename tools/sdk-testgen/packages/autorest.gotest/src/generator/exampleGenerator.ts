/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import { BaseCodeGenerator } from './baseGenerator';
import { Config } from '../common/constant';
import { ExampleModel, MockTestDefinitionModel } from '@autorest/testmodeler/dist/src/core/model';
import { GoExampleModel } from '../common/model';
import { MockTestDataRender } from './mockTestGenerator';
import { getAPIParametersSig, getClientParametersSig } from '../util/codegenBridge';

export class ExampleDataRender extends MockTestDataRender {
    protected fillExampleOutput(example: GoExampleModel) {
        const op = example.operation;
        example.methodParametersPlaceholderOutput = this.toParametersOutput(getAPIParametersSig(op), example.methodParameters);
        example.clientParametersPlaceholderOutput = this.toParametersOutput(getClientParametersSig(example.operationGroup), example.clientParameters);
    }
}

export class ExampleCodeGenerator extends BaseCodeGenerator {
    public generateCode(extraParam: Record<string, unknown> = {}): void {
        for (const [_, exampleGroups] of Object.entries(MockTestDefinitionModel.groupByOperationGroup(this.context.codeModel.testModel.mockTest.exampleGroups))) {
            let exampleModel: ExampleModel = null;
            for (const exampleGroup of exampleGroups) {
                if (exampleGroup.examples.length > 0) {
                    exampleModel = exampleGroup.examples[0];
                    break;
                }
            }
            if (exampleModel === null) {
                continue;
            }

            this.renderAndWrite(
                { exampleGroups: exampleGroups },
                'exampleTest.go.njk',
                `${this.getFilePrefix(Config.exampleFilePrefix)}example_${exampleModel.operationGroup.language.go.name.toLowerCase()}_client_test.go`,
                extraParam,
            );
        }
    }
}
