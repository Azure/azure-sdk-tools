/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import * as nunjucks from 'nunjucks';
import * as path from 'path';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { Host } from '@autorest/extension-base';
import { TestCodeModel } from '@autorest/testmodeler/dist/src/core/model';
import { TestConfig } from '@autorest/testmodeler/dist/src/common/testConfig';
import { TestDefinitionFile } from 'oav/dist/lib/testScenario/testResourceTypes';
export abstract class TestGenerator {
    private allVariables = [];
    public constructor(public host: Host, public codeModel: TestCodeModel, public testConfig: TestConfig) {
        if (!(testConfig instanceof TestConfig)) {
            this.testConfig = new TestConfig(testConfig);
        }
    }

    abstract getMockTestFilename(): string;
    abstract getScenarioTestFilename(testDef: TestDefinitionFile): string;
    abstract genRenderDataForMock();
    abstract genRenderDataForDefinition(testDef: TestDefinitionFile);
    genRenderData() {
        this.genRenderDataForMock();

        for (const testDef of this.codeModel.testModel.scenarioTests) {
            this.genRenderDataForDefinition(testDef);
        }
    }

    public async generateMockTest(templateFile: string, extraParam: any = {}) {
        const tmplPath = path.relative(process.cwd(), path.join(`${__dirname}`, `../../src/template/${templateFile}`));

        const output = this.render(tmplPath, {
            ...this.codeModel.testModel.mockTest,
            config: this.testConfig.config,
            ...extraParam,
        });
        this.writeToHost(this.getMockTestFilename(), output);
    }

    public async generateScenarioTest(templateFile: string, extraParam: any = {}) {
        for (const testDef of this.codeModel.testModel.scenarioTests) {
            const tmplPath = path.relative(process.cwd(), path.join(`${__dirname}`, `../../src/template/${templateFile}`));

            const output = this.render(tmplPath, {
                ...testDef,
                config: this.testConfig.config,
                testCaseName: Helper.capitalize(Helper.toCamelCase(path.basename(testDef._filePath).split('.').slice(0, -1).join('.'))),
                ...extraParam,
            });
            this.writeToHost(this.getScenarioTestFilename(testDef), output);
        }
    }

    public writeToHost(fileName: string, output: string) {
        this.host.WriteFile(fileName, output, undefined);
    }

    public render(tmplPath: string, data: any): string {
        nunjucks.configure({ autoescape: false });
        return nunjucks.render(tmplPath, {
            ...data,
            allVariables: this.allVariables,
            jsFunc: {
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
                    return Helper.uncapitalize(typeName.split('.').join('*').split('*').last);
                },
            },
        });
    }

    // Pick out $(...) variables from normal string
    // For example: "a string with ${var} inside" => ['"a string with "', 'var', '" inside"']
    public parseOavVariable(s: string, definedVariables: Record<string, string>): string[] {
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
            s = s.substr(p + placeHolder.length);
        }
        if (s.length > 0) {
            ret.push(Helper.quotedEscapeString(s));
        }

        return ret;
    }
}
