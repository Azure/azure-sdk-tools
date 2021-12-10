import * as path from 'path';
import { Config } from '../common/constant';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';
import { Host } from '@autorest/extension-base';
import { TestConfig } from '@autorest/testmodeler/dist/src/common/testConfig';

export async function processRequest(host: Host): Promise<void> {
    const testConfig = new TestConfig(await host.GetValue(''));
    const files = await host.ListInputs();
    Helper.execSync(`go get golang.org/x/tools/cmd/goimports`);
    for (const outputFile of files) {
        if (outputFile.endsWith('.go')) {
            const pathName = path.join(testConfig.getValue(Config.outputFolder), outputFile);
            Helper.execSync(`go fmt ${pathName}`);
            Helper.execSync(`goimports -w ${pathName}`);
        }
    }
}
