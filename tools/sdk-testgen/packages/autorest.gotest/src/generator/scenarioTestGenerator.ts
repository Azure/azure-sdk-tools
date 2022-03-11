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
import { OutputVariableModelType, StepRestCallModel, TestDefinitionModel, TestScenarioModel } from '@autorest/testmodeler/dist/src/core/model';
import { Step } from 'oav/dist/lib/apiScenario/apiScenarioTypes';

export class ScenarioTestDataRender extends MockTestDataRender {
    parentVariables: Record<string, string> = {};
    currentVariables: Record<string, string> = {};
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
            this.parentVariables = {
                ...testDef.requiredVariablesDefault,
                ...testDef.variables,
            };
            this.currentVariables = {
                ...step.variables,
            };
            this.genRenderDataForStep(step);
        }
        for (const scenario of testDef.scenarios as TestScenarioModel[]) {
            if (scenario.scenario === undefined) {
                scenario.scenario = scenario.description;
            }
            this.scenarioReferencedVariables = new Set<string>();
            for (const step of scenario.steps) {
                this.stepReferencedVariables = new Set<string>();
                // inner variable should overwrite outer ones
                this.parentVariables = {
                    ...testDef.requiredVariablesDefault,
                    ...testDef.variables,
                    ...scenario.requiredVariablesDefault,
                    ...scenario.variables,
                };
                this.currentVariables = {
                    ...step.variables,
                };
                this.genRenderDataForStep(step);
            }

            // remove useless variable
            for (const variableName of Object.keys(scenario.variables || {})) {
                if (!this.scenarioReferencedVariables.has(variableName)) {
                    delete scenario.variables[variableName];
                }
            }

            // resolve scenario variables
            this.parentVariables = {
                ...testDef.requiredVariablesDefault,
                ...testDef.variables,
            };
            this.currentVariables = {
                ...scenario.requiredVariablesDefault,
                ...scenario.variables,
            };
            for (const [key, value] of Object.entries(scenario.variables || {})) {
                scenario.variables[key] = this.getStringValue(value);
                if (key === scenario.variables[key] && !Object.prototype.hasOwnProperty.call(this.parentVariables, key)) {
                    scenario.variables[key] = '<newDefinedVariable>';
                }
            }
        }
        for (const step of testDef.cleanUpSteps) {
            // inner variable should overwrite outer ones
            this.parentVariables = {
                ...testDef.requiredVariablesDefault,
                ...testDef.variables,
            };
            this.currentVariables = {
                ...step.variables,
            };
            this.genRenderDataForStep(step);
        }

        // resolve scope variables
        this.parentVariables = {};
        this.currentVariables = {
            ...testDef.requiredVariablesDefault,
            ...testDef.variables,
        };
        for (const [key, value] of Object.entries(testDef.variables || {})) {
            testDef.variables[key] = this.getStringValue(value);
            if (key === testDef.variables[key]) {
                testDef.variables[key] = '<newDefinedVariable>';
            }
        }
    }

    private genRenderDataForStep(step: Step) {
        switch (step.type) {
            case OavStepType.restCall: {
                const example = (step as StepRestCallModel).exampleModel as GoExampleModel;
                // request and response parse
                this.fillExampleOutput(example);

                // response output variable convert
                if (step.outputVariables && Object.keys(step.outputVariables).length > 0) {
                    example.checkResponse = true;
                    for (const [variableName, variableConfig] of Object.entries((step as StepRestCallModel).outputVariablesModel)) {
                        let isPtr = false;
                        for (let i = 0; i < variableConfig.length; i++) {
                            if (variableConfig[i].type === OutputVariableModelType.object) {
                                variableConfig[i]['languageName'] = `.${variableConfig[i].languages.go.name}`;
                                isPtr = !variableConfig[i].languages.go?.byValue;
                            } else if (variableConfig[i].type === OutputVariableModelType.index) {
                                variableConfig[i]['languageName'] = `[${variableConfig[i].index}]`;
                            } else if (variableConfig[i].type === OutputVariableModelType.key) {
                                variableConfig[i]['languageName'] = `["${variableConfig[i].key}"]`;
                            }
                        }
                        step.outputVariables[variableName]['isPtr'] = isPtr;
                    }
                }
                break;
            }
            case OavStepType.armTemplate: {
                // environment variables & arguments parse
                if (step.armTemplatePayload.resources) {
                    step.armTemplatePayload.resources.forEach((t) => {
                        (t.properties['environmentVariables'] || []).forEach((e) => {
                            if (e.value) {
                                e.value = '<parsedVariable>' + this.getStringValue(e.value);
                            } else {
                                e.secureValue = '<parsedVariable>' + this.getStringValue(e.secureValue);
                            }
                        });
                        if (t.properties['arguments']) {
                            t.properties['arguments'] = this.getStringValue(t.properties['arguments']);
                        }
                    });
                }

                // template parse
                step['armTemplateOutput'] = GoHelper.obejctToString(step.armTemplatePayload);

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
            if (key === step.variables[key] && !Object.prototype.hasOwnProperty.call(this.parentVariables, key)) {
                step.variables[key] = '<newDefinedVariable>';
            }
        }
    }

    protected getStringValue(rawValue: string) {
        if (typeof rawValue === 'string') {
            return this.parseOavVariable(rawValue, { ...this.parentVariables, ...this.currentVariables }).join(' + ');
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
        if (this.context.codeModel.testModel.scenarioTests.length > 0) {
            this.renderAndWrite({}, 'scenarioUtil.go.njk', `scenario_test/${this.getFilePrefix(Config.testFilePrefix)}scenario_test_util.go`, extraParam);
            for (const testDef of this.context.codeModel.testModel.scenarioTests) {
                const file = path.basename(testDef._filePath);
                const filename = file.split('.').slice(0, -1).join('.');
                const rpRegex = /(\/|\\)(?<rpName>[^/\\]+)(\/|\\)(arm[^/\\]+)/;
                const execResult = rpRegex.exec(this.context.testConfig.getValue(Config.outputFolder));
                extraParam['rpName'] = execResult?.groups ? execResult.groups['rpName'] : '';
                extraParam['testPackageName'] = filename.toLowerCase();
                this.renderAndWrite(
                    { ...testDef, testCaseName: Helper.capitalize(Helper.toCamelCase(filename)), allVariables: [] },
                    'scenarioTest.go.njk',
                    `scenario_test/${filename.toLowerCase()}/${this.getFilePrefix(Config.testFilePrefix)}${filename.toLowerCase()}_test.go`,
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
