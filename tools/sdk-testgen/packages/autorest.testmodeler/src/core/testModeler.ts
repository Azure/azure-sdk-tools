/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import * as _ from 'lodash';
import { AutorestExtensionHost } from '@autorest/extension-base';
import { Config, configDefaults } from '../common/constant';
import { Helper } from '../util/helper';
import { TestCodeModeler } from './model';
import { TestConfig } from '../common/testConfig';

export async function processRequest(host: AutorestExtensionHost): Promise<void> {
    //const session = await startSession<TestCodeModel>(host, {}, codeModelSchema)
    const session = await TestCodeModeler.getSessionFromHost(host);
    const config = new TestConfig(await session.getValue(''), configDefaults);

    if (config.getValue(Config.exportCodemodel)) {
        Helper.addCodeModelDump(session, 'test-modeler-pre.yaml', false);
    }
    // const files = await session.listInputs()
    // const codemodel = await session.readFile('code-model-v4.yaml')

    const codeModel = TestCodeModeler.createInstance(session.model, config);
    codeModel.genMockTests(session);
    await codeModel.loadTestResources(session);

    await Helper.outputToModelerfour(host, session, config.getValue(Config.exportExplicitType), config.getValue(Config.explicitTypes));
    if (config.getValue(Config.exportCodemodel)) {
        Helper.addCodeModelDump(session, 'test-modeler.yaml', false);
        if (config.getValue(Config.exportExplicitType)) {
            Helper.addCodeModelDump(session, 'test-modeler-with-tags.yaml', true);
        }
    }
    await Helper.dump(host);
}
