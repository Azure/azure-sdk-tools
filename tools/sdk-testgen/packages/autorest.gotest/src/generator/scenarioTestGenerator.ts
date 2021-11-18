/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import * as _ from 'lodash';
import * as path from 'path';
import { BaseCodeGenerator } from './baseGenerator';
import { Config } from '../common/constant';
import { GoExampleModel } from '../common/model';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { MockTestDataRender } from './mockTestGenerator';
import { OavStepType } from '@autorest/testmodeler/dist/src/common/constant';
import { Step } from 'oav/dist/lib/apiScenario/apiScenarioTypes';
import { StepArmTemplateModel, StepRestCallModel, TestDefinitionModel, TestScenarioModel } from '@autorest/testmodeler/dist/src/core/model';

export class ScenarioTestDataRender extends MockTestDataRender {
    definedVariables: Record<string, string> = {};
    referencedVariables: Set<string> = new Set<string>();

    public renderData() {
        for (const testDef of this.context.codeModel.testModel.scenarioTests) {
            this.generateScenarioTestData(testDef);
        }
    }

    private generateScenarioTestData(testDef: TestDefinitionModel) {
        if (testDef.scope.toLowerCase() === 'resourcegroup') {
            this.context.importManager.add('github.com/Azure/azure-sdk-for-go/sdk/resources/armresources');
        }

        for (const step of testDef.prepareSteps) {
            this.definedVariables = {
                ...testDef.requiredVariablesDefault,
                ...testDef.variables,
            };
            this.genRenderDataForStep(testDef, step);
        }
        for (const scenario of testDef.scenarios as TestScenarioModel[]) {
            this.referencedVariables = new Set<string>();
            for (const step of scenario.steps) {
                this.definedVariables = {
                    ...testDef.requiredVariablesDefault,
                    ...scenario.requiredVariablesDefault,
                    ...testDef.variables,
                    ...scenario.variables,
                    ...step.variables,
                };
                this.genRenderDataForStep(testDef, step);
            }
            for (const v in scenario.variables) {
                if (!this.referencedVariables.has(v)) {
                    delete scenario.variables[v];
                }
            }
        }
    }

    private genRenderDataForStep(testDef: TestDefinitionModel, step: Step) {
        switch (step.type) {
            case OavStepType.restCall: {
                const example = (step as StepRestCallModel).exampleModel as GoExampleModel;
                this.fillExampleOutput(example);
                if (step.outputVariables && Object.keys(step.outputVariables).length > 0) {
                    this.context.importManager.add('github.com/go-openapi/jsonpointer');
                    example.checkResponse = true;
                }
                break;
            }
            case OavStepType.armTemplate: {
                testDef.useArmTemplate = true;
                this.context.importManager.add('encoding/json');
                (step as StepArmTemplateModel).outputVariableNames = [];
                for (const templateOutput of Object.keys(step.armTemplatePayload.outputs || {})) {
                    if (_.has(testDef.variables, templateOutput) || _.has(step.variables, templateOutput)) {
                        (step as StepArmTemplateModel).outputVariableNames.push(templateOutput);
                    }
                }
                break;
            }
            default:
        }
    }

    protected getStringValue(rawValue: string) {
        if (typeof rawValue === 'string') {
            return this.parseOavVariable(rawValue, this.definedVariables).join(' + ');
        } else {
            return Helper.quotedEscapeString(rawValue);
        }
    }

    // Pick out $(...) variables from normal string
    // For example: "a string with ${var} inside" => ['"a string with "', 'var', '" inside"']
    private parseOavVariable(s: string, definedVariables: Record<string, string>): string[] {
        if (!s) {
            return ['""'];
        }

        const re = /\$\([^)]+\)/g;
        const ret: string[] = [];
        const m = s.match(re);
        let placeHolders = [];

        if (m) {
            placeHolders = m.map((x) => x.toString());
        }
        for (const placeHolder of placeHolders.filter((x) => Object.prototype.hasOwnProperty.call(definedVariables, x.slice(2, -1)))) {
            const p = s.indexOf(placeHolder);
            if (p > 0) {
                ret.push(Helper.quotedEscapeString(s.substr(0, p)));
            }
            ret.push(placeHolder.slice(2, -1));
            this.referencedVariables.add(ret.slice(-1)[0]);
            s = s.substr(p + placeHolder.length);
        }
        if (s.length > 0) {
            ret.push(Helper.quotedEscapeString(s));
        }

        return ret;
    }
}

export class ScenarioTestCodeGenerator extends BaseCodeGenerator {
    public generateCode(extraParam: Record<string, unknown> = {}): void {
        for (const testDef of this.context.codeModel.testModel.scenarioTests) {
            const file = path.basename(testDef._filePath);
            const filename = file.split('.').slice(0, -1).join('.');
            this.renderAndWrite(
                { ...testDef, testCaseName: Helper.capitalize(Helper.toCamelCase(filename)), allVariables: [] },
                'scenarioTest.go.njk',
                `scenario/${this.getFilePrefix(Config.testFilePrefix)}${filename}_test.go`,
                extraParam,
                {
                    toSnakeCase: Helper.toSnakeCase,
                    uncapitalize: Helper.uncapitalize,
                    descToTestname: (description: string) => Helper.capitalize(Helper.toCamelCase(description)).slice(0, 50),
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
