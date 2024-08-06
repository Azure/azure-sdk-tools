import { Recorder, VitestTestContext } from "@azure-tools/test-recorder";
/**
 * creates the recorder and reads the environment variables from the `.env` file.
 * Should be called first in the test suite to make sure environment variables are
 * read before they are being used.
 */
export declare function createRecorder(context: VitestTestContext): Promise<Recorder>;
//# sourceMappingURL=recordedClient.d.ts.map