/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import * as _ from 'lodash';
import * as fs from 'fs';
import * as path from 'path';
import { BaseCodeGenerator } from './baseGenerator';
import { Config } from '../common/constant';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { ScenarioTestDataRender } from './scenarioTestGenerator';
import { TestDefinitionModel } from '@autorest/testmodeler/dist/src/core/model';

export class SampleDataRender extends ScenarioTestDataRender {
    packagePrefixForGlobalVariables = '';

    public renderData() {
        for (const testDef of this.context.codeModel.testModel.scenarioTests) {
            this.generateSampleData(testDef);
        }
    }

    protected generateSampleData(testDef: TestDefinitionModel) {
        super.generateScenarioTestData(testDef);
    }
}

export class SampleCodeGenerator extends BaseCodeGenerator {
    public generateCode(extraParam: Record<string, unknown> = {}): void {
        if (this.context.codeModel.testModel.scenarioTests.length > 0) {
            for (const testDef of this.context.codeModel.testModel.scenarioTests) {
                const file = path.basename(testDef._filePath);
                const filename = file.split('.').slice(0, -1).join('.');

                const rpRegex = /(\/|\\)(?<rpName>[^/\\]+)(\/|\\)(arm[^/\\]+)/;
                const execResult = rpRegex.exec(this.context.testConfig.getValue(Config.outputFolder));
                extraParam['rpName'] = execResult?.groups ? execResult.groups['rpName'] : '';
                extraParam['testPackageName'] = filename.toLowerCase();

                const homePath = path.join(this.context.testConfig.getValue(Config.outputFolder), extraParam['testPackageName'] as string);
                if (!fs.existsSync(homePath)) {
                    fs.mkdirSync(homePath, { recursive: true });
                }
                this.renderAndWrite({ ...testDef }, 'sampleGo.mod.njk', `${extraParam['testPackageName']}/go.mod`, extraParam);

                this.renderAndWrite(
                    { ...testDef, testCaseName: Helper.capitalize(Helper.toCamelCase(filename)) },
                    'sampleMain.go.njk',
                    `${extraParam['testPackageName']}/main.go`,
                    extraParam,
                    {
                        toSnakeCase: Helper.toSnakeCase,
                        uncapitalize: Helper.uncapitalize,
                        toCamelCase: Helper.toCamelCase,
                        capitalize: Helper.capitalize,
                        quotedEscapeString: Helper.quotedEscapeString,
                        genVariableName: (typeName: string) => {
                            // This function generate variable instance name from variable type name
                            // For instance:
                            //   1) VirtualMachineResponse  --> virtualMachineResponse
                            //   2) armCompute.VirtualMachineResponse  --> virtualMachineResponse   // remove package name
                            //   3) *VirtualMachineResponse  --> virtualMachineResponse  // remove char of pointer.
                            return Helper.uncapitalize(typeName.split('.').join('*').split('*').pop());
                        },
                    },
                );
            }
        }
    }
}
