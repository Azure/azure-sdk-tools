import {RunningEnvironment} from "./runningEnvironment";

export function getOutputPackageInfo(runningEnvironment: RunningEnvironment | undefined, readmeMd: string | undefined, typespecProject: string | undefined) {
    let outputPackageInfo: any;
    if (runningEnvironment === RunningEnvironment.SwaggerSdkAutomation) {
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
            "artifacts": [],
            "result": "succeeded"
        };
        if (typespecProject) {
            outputPackageInfo['typespecProject'] = [typespecProject];
        } else {
            outputPackageInfo['readmeMd'] = [readmeMd];
        }
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
