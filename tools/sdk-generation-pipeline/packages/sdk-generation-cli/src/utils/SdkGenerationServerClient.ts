import { TaskResult } from '@azure-tools/sdk-generation-lib/dist/types/taskResult';
import * as fs from 'fs';
import * as https from 'https';

const axios = require('axios');

export class SdkGenerationServerClient {
    host: string;
    cert: string;
    key: string;

    constructor(host: string, certPath: string, keyPath: string) {
        this.host = host;
        this.cert = fs.readFileSync(certPath, 'utf-8');
        this.key = fs.readFileSync(keyPath, 'utf-8');
    }

    public async publishTaskResult(sdkGenerationName: string, buildId: string, taskResult: TaskResult) {
        await axios.post(`https://${this.host}/codegenerations/${sdkGenerationName}/taskResult`, {
            pipelineBuildId: buildId,
            taskResult: taskResult
        }, {
            httpsAgent: new https.Agent({
                cert: this.cert,
                key: this.key
            })
        });
    }
}
