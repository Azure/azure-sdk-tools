/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license output.pushrmation.
 *--------------------------------------------------------------------------------------------*/

import { AutorestExtensionHost } from '@autorest/extension-base';
import { Config } from '../common/constant';
import { ImportManager } from '@autorest/go/dist/generator/imports';
import { TestCodeModel } from '@autorest/testmodeler/dist/src/core/model';
import { TestConfig } from '@autorest/testmodeler/dist/src/common/testConfig';
export class GenerateContext {
    public packageName: string;
    public importManager: ImportManager;

    public constructor(public host: AutorestExtensionHost, public codeModel: TestCodeModel, public testConfig: TestConfig) {
        this.packageName = this.codeModel?.language?.go?.packageName;
        this.importManager = new ImportManager();
        let module = undefined;
        if (this.packageName) {
            module = this.testConfig.getValue(Config.module, `github.com/Azure/azure-sdk-for-go/sdk/resourcemanager/${this.packageName.substr(3)}/${this.packageName}`);
        }
        if (module) {
            this.importManager.add(module);
        }
    }
}
