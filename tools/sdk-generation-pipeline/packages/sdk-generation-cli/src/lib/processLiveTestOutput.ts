import {RunLiveTestTaskCliConfig} from "../cliSchema/runLiveTestTaskCliConfig";
import * as fs from "fs";

export async function processLiveTestOutput(config: RunLiveTestTaskCliConfig): Promise<boolean> {
    if (!fs.existsSync(config.liveTestOutputJson)) return true;
    // TODO: parse the result and show them
}
