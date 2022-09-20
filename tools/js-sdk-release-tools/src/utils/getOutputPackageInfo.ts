import {RunningEnvironment} from "./runningEnvironment";

export function getOutputPackageInfo(runningEnvironment: RunningEnvironment | undefined, readmeMd?: string) {
    let outputPackageInfo: any;
    if (runningEnvironment === RunningEnvironment.SwaggerSdkAutomation) {
        outputPackageInfo = {
            "packageName": "",
            "path": [
                'rush.json',
                'common/config/rush/pnpm-lock.yaml'
            ],
            "readmeMd": [
                readmeMd
            ],
            "changelog": {
                "content": "",
                "hasBreakingChange": false
            },
            "artifacts": [],
            "result": "succeeded"
        };
    } else if (runningEnvironment === RunningEnvironment.SdkGeneration) {
        outputPackageInfo = {
            "packageName": "",
            "path": [
                'rush.json',
                'common/config/rush/pnpm-lock.yaml'
            ],
            "changelog": {
                "content": "",
                "hasBreakingChange": false
            },
            "packageFolder": '',
            "artifacts": [],
            "result": "succeeded"
        }
    }
    return outputPackageInfo;
}
