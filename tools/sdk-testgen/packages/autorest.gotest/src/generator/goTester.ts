/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import * as _ from 'lodash';
import { Config } from '../common/constant';
import { ExampleCodeGenerator, ExampleDataRender } from './exampleGenerator';
import { GenerateContext } from './generateContext';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { Host } from '@autorest/extension-base';
import { MockTestCodeGenerator, MockTestDataRender } from './mockTestGenerator';
import { ScenarioTestCodeGenerator, ScenarioTestDataRender } from './scenarioTestGenerator';
import { TestCodeModeler } from '@autorest/testmodeler/dist/src/core/model';
import { TestConfig } from '@autorest/testmodeler/dist/src/common/testConfig';

export async function processRequest(host: Host): Promise<void> {
    const session = await TestCodeModeler.getSessionFromHost(host);
    const config = await session.getValue('');
    if (_.get(config, Config.exportCodemodel, false)) {
        Helper.addCodeModelDump(session, 'go-tester-pre.yaml');
    }

    const context = new GenerateContext(host, session.model, new TestConfig(config));
    const mockTestDataRender = new MockTestDataRender(context);
    mockTestDataRender.renderData();

    const extraParam = { copyright: await Helper.getCopyright(session) };
    if (_.get(config, Config.generateMockTest, true)) {
        const mockTestCodeGenerator = new MockTestCodeGenerator(context);
        mockTestCodeGenerator.generateCode(extraParam);
    }
    if (_.get(config, Config.generateSdkExample, false)) {
        const exampleDataRender = new ExampleDataRender(context);
        exampleDataRender.renderData();
        const exampleCodeGenerator = new ExampleCodeGenerator(context);
        exampleCodeGenerator.generateCode(extraParam);
    }
    if (_.get(config, Config.generateScenarioTest, false)) {
        const scenarioTestDataRender = new ScenarioTestDataRender(context);
        scenarioTestDataRender.renderData();
        const scenarioTestCodeGenerator = new ScenarioTestCodeGenerator(context);
        scenarioTestCodeGenerator.generateCode(extraParam);
    }
    await Helper.outputToModelerfour(host, session);
    if (_.get(config, Config.exportCodemodel, false)) {
        Helper.addCodeModelDump(session, 'go-tester.yaml');
    }
    Helper.dump(host);
}
