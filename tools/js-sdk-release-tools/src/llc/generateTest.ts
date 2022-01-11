import * as fs from "fs";
import * as path from "path";
import {logger} from "../utils/logger";
import {createFolderIfNotExist} from "./utils";

function generateEnvFile(packagePath) {
    const content = `// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

import * as dotenv from "dotenv";

dotenv.config();`;
    fs.writeFileSync(path.join(packagePath, 'test', 'public', 'utils', 'env.ts'), content, {encoding: 'utf-8'});
}

function generateEnvBrowserFile(packagePath) {
    const content = `// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.`;
    fs.writeFileSync(path.join(packagePath, 'test', 'public', 'utils', 'env.browser.ts'), content, {encoding: 'utf-8'});
}

function generateRecordedClientFile(packagePath) {
    const content = `// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

/// <reference lib="esnext.asynciterable" />

import { Context } from "mocha";

import { Recorder, record, RecorderEnvironmentSetup } from "@azure-tools/test-recorder";

import "./env";

const replaceableVariables: { [k: string]: string } = {
  ENDPOINT: "endpoint",
  AZURE_CLIENT_ID: "azure_client_id",
  AZURE_CLIENT_SECRET: "azure_client_secret",
  AZURE_TENANT_ID: "88888888-8888-8888-8888-888888888888",
};

export const environmentSetup: RecorderEnvironmentSetup = {
  replaceableVariables,
  customizationsOnRecordings: [
    (recording: string): string =>
      recording.replace(/"access_token"\\s?:\\s?"[^"]*"/g, \`"access_token":"access_token"\`),
    // If we put ENDPOINT in replaceableVariables above, it will not capture
    // the endpoint string used with nock, which will be expanded to
    // https://<endpoint>:443/ and therefore will not match, so we have to do
    // this instead.
    (recording: string): string => {
      const replaced = recording.replace("endpoint:443", "endpoint");
      return replaced;
    },
  ],
  queryParametersToSkip: [],
}

/**
 * creates the recorder and reads the environment variables from the \`.env\` file.
 * Should be called first in the test suite to make sure environment variables are
 * read before they are being used.
 */
export function createRecorder(context: Context): Recorder {
  return record(context, environmentSetup);
}`;
    fs.writeFileSync(path.join(packagePath, 'test', 'public', 'utils', 'recordedClient.ts'), content, {encoding: 'utf-8'});
}

function generateSpecFile(packagePath) {
    const content = `// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
import { Recorder } from "@azure-tools/test-recorder";

import { createRecorder } from "./utils/recordedClient";
import { Context } from "mocha";
import { assert } from "chai";

describe("Sample test", () => {
  let recorder: Recorder;

  beforeEach(function (this: Context) {
    recorder = createRecorder(this);
  });

  afterEach(async function () {
    await recorder.stop();
  });

  it("sample test", async function() {
    assert.equal(1, 1);
  });
});`;
    fs.writeFileSync(path.join(packagePath, 'test', 'public', 'sample.spec.ts'), content, {encoding: 'utf-8'});
}

export function generateTest(packagePath) {
    if (fs.existsSync(path.join(packagePath, 'test'))) {
        logger.logGreen(`test folder already exists, and we don't generate sample test.`)
        return;
    }
    logger.logGreen(`Generating sample test`);
    createFolderIfNotExist(path.join(packagePath, 'test'));
    createFolderIfNotExist(path.join(packagePath, 'test', 'public'));
    createFolderIfNotExist(path.join(packagePath, 'test', 'public', 'utils'));
    generateSpecFile(packagePath);
    generateEnvFile(packagePath);
    generateEnvBrowserFile(packagePath);
    generateRecordedClientFile(packagePath);
}
