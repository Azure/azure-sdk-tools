import {RunMockTestTaskCliConfig} from "../cliSchema/runMockTestTaskCliConfig";
import * as fs from "fs";

export async function processMockTestOutput(config: RunMockTestTaskCliConfig): Promise<boolean> {
    if (!fs.existsSync(config.mockTestOutputJson)) return true;
    // TODO: parse the result and show them
}
