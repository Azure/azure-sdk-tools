/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import * as _ from 'lodash';
import * as path from 'path';
import { BaseCodeGenerator } from './baseGenerator';
import { Config } from '../common/constant';
import { GoExampleModel } from '../common/model';
import { GoHelper } from '../util/goHelper';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { MockTestDataRender } from './mockTestGenerator';
import { OavStepType } from '@autorest/testmodeler/dist/src/common/constant';
import { Step } from 'oav/dist/lib/apiScenario/apiScenarioTypes';
import { StepArmTemplateModel, StepRestCallModel, TestDefinitionModel, TestScenarioModel } from '@autorest/testmodeler/dist/src/core/model';

export class ScenarioTestDataRender extends MockTestDataRender {
    definedVariables: Record<string, string> = {};
    scenarioReferencedVariables: Set<string> = new Set<string>();
    stepReferencedVariables: Set<string> = new Set<string>();

    public renderData() {
        for (const testDef of this.context.codeModel.testModel.scenarioTests) {
            this.generateScenarioTestData(testDef);
        }
    }

    private generateScenarioTestData(testDef: TestDefinitionModel) {
        if (testDef.scope.toLowerCase() === 'resourcegroup') {
            this.context.importManager.add('github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/resources/armresources');
        }

        for (const step of testDef.prepareSteps) {
            // inner variable should overwrite outer ones
            this.definedVariables = {
                ...testDef.requiredVariablesDefault,
                ...testDef.variables,
                ...step.variables,
            };
            this.genRenderDataForStep(testDef, step);
        }
        for (const scenario of testDef.scenarios as TestScenarioModel[]) {
            if (scenario.scenario === undefined) {
                scenario.scenario = scenario.description;
            }
            this.scenarioReferencedVariables = new Set<string>();
            for (const step of scenario.steps) {
                this.stepReferencedVariables = new Set<string>();
                // inner variable should overwrite outer ones
                this.definedVariables = {
                    ...testDef.requiredVariablesDefault,
                    ...testDef.variables,
                    ...scenario.requiredVariablesDefault,
                    ...scenario.variables,
                    ...step.variables,
                };
                this.genRenderDataForStep(testDef, step);
            }

            // remove useless variable
            for (const variableName of Object.keys(scenario.variables || {})) {
                if (!this.scenarioReferencedVariables.has(variableName)) {
                    delete scenario.variables[variableName];
                }
            }

            // resolve scenario variables
            this.definedVariables = {
                ...testDef.requiredVariablesDefault,
                ...testDef.variables,
                ...scenario.requiredVariablesDefault,
                ...scenario.variables,
            };
            for (const [key, value] of Object.entries(scenario.variables || {})) {
                scenario.variables[key] = this.getStringValue(value);
            }
        }
        for (const step of testDef.cleanUpSteps) {
            // inner variable should overwrite outer ones
            this.definedVariables = {
                ...testDef.requiredVariablesDefault,
                ...testDef.variables,
                ...step.variables,
            };
            this.genRenderDataForStep(testDef, step);
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
                // for arm template, all string parameter should be considered as input variable
                (step as StepArmTemplateModel).inputVariableNames = [];
                for (const parameterName of Object.keys(step.armTemplatePayload.parameters || {})) {
                    if (_.has(this.definedVariables, parameterName) && step.armTemplatePayload.parameters[parameterName].type === 'string') {
                        (step as StepArmTemplateModel).inputVariableNames.push(parameterName);
                    }
                }
                // for arm template, all string output should be considered as output variable
                (step as StepArmTemplateModel).outputVariableNames = [];
                for (const outputName of Object.keys(step.armTemplatePayload.outputs || {})) {
                    if (_.has(this.definedVariables, outputName) && step.armTemplatePayload.outputs[outputName].type === 'string') {
                        (step as StepArmTemplateModel).outputVariableNames.push(outputName);
                    }
                }
                step['armTemplateOutput'] = GoHelper.obejctToString(step.armTemplatePayload);
                // add all environment variable
                const environments = [];
                if (step.armTemplatePayload.resources) {
                    step.armTemplatePayload.resources.forEach((t) => {
                        (t.properties['environmentVariables'] || []).forEach((e) => {
                            environments.push({
                                key: e.name,
                                value: this.getStringValue(e.value || e.secureValue),
                            });
                        });
                    });
                }
                step['environmentVaribles'] = environments;
                break;
            }
            default:
        }
        // remove useless variable
        for (const variableName of Object.keys(step.variables || {})) {
            if (!this.stepReferencedVariables.has(variableName)) {
                delete step.variables[variableName];
            }
        }
        // resolve step variables
        for (const [key, value] of Object.entries(step.variables || {})) {
            step.variables[key] = this.getStringValue(value);
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
                ret.push(Helper.quotedEscapeString(s.substring(0, p)));
            }
            ret.push(placeHolder.slice(2, -1));
            this.scenarioReferencedVariables.add(_.last(ret));
            this.stepReferencedVariables.add(_.last(ret));
            s = s.substring(p + placeHolder.length);
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
                `scenario/${this.getFilePrefix(Config.testFilePrefix)}${filename.toLowerCase()}_test.go`,
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
