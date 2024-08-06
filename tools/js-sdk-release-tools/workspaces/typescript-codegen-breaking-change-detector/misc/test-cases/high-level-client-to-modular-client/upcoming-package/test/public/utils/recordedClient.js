// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
import { Recorder, } from "@azure-tools/test-recorder";
const replaceableVariables = {
    SUBSCRIPTION_ID: "azure_subscription_id"
};
const recorderEnvSetup = {
    envSetupForPlayback: replaceableVariables,
    removeCentralSanitizers: [
        "AZSDK3493", // .name in the body is not a secret and is listed below in the beforeEach section
        "AZSDK3430", // .id in the body is not a secret and is listed below in the beforeEach section
    ],
};
/**
 * creates the recorder and reads the environment variables from the `.env` file.
 * Should be called first in the test suite to make sure environment variables are
 * read before they are being used.
 */
export async function createRecorder(context) {
    const recorder = new Recorder(context);
    await recorder.start(recorderEnvSetup);
    return recorder;
}
//# sourceMappingURL=recordedClient.js.map