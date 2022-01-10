/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import * as _ from 'lodash';
import { AutorestExtensionHost } from '@autorest/extension-base';
import { Config, configDefaults } from '../common/constant';
import { ExampleCodeGenerator, ExampleDataRender } from './exampleGenerator';
import { GenerateContext } from './generateContext';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { MockTestCodeGenerator, MockTestDataRender } from './mockTestGenerator';
import { ScenarioTestCodeGenerator, ScenarioTestDataRender } from './scenarioTestGenerator';
import { TestCodeModeler } from '@autorest/testmodeler/dist/src/core/model';
import { TestConfig } from '@autorest/testmodeler/dist/src/common/testConfig';

export async function processRequest(host: AutorestExtensionHost): Promise<void> {
    const session = await TestCodeModeler.getSessionFromHost(host);
    const config = new TestConfig(await session.getValue(''), configDefaults);
    if (config.getValue(Config.exportCodemodel)) {
        Helper.addCodeModelDump(session, 'go-tester-pre.yaml');
    }

    const context = new GenerateContext(host, session.model, config);
    const mockTestDataRender = new MockTestDataRender(context);
    mockTestDataRender.renderData();

    const extraParam = {
        copyright: await Helper.getCopyright(session),
        sendExampleId: config.getValue(Config.sendExampleId),
        verifyResponse: config.getValue(Config.verifyResponse),
    };
    if (config.getValue(Config.generateMockTest)) {
        const mockTestCodeGenerator = new MockTestCodeGenerator(context);
        mockTestCodeGenerator.generateCode(extraParam);
    }
    if (config.getValue(Config.generateSdkExample)) {
        const exampleDataRender = new ExampleDataRender(context);
        exampleDataRender.renderData();
        const exampleCodeGenerator = new ExampleCodeGenerator(context);
        exampleCodeGenerator.generateCode(extraParam);
    }
    if (config.getValue(Config.generateScenarioTest)) {
        const scenarioTestDataRender = new ScenarioTestDataRender(context);
        scenarioTestDataRender.renderData();
        const scenarioTestCodeGenerator = new ScenarioTestCodeGenerator(context);
        scenarioTestCodeGenerator.generateCode(extraParam);
    }
    await Helper.outputToModelerfour(host, session);
    if (config.getValue(Config.exportCodemodel)) {
        Helper.addCodeModelDump(session, 'go-tester.yaml');
    }
    Helper.dump(host);
}
