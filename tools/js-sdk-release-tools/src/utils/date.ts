import {NPMViewResult, StringMap, tr} from "@ts-common/azure-js-dev-tools";
import {logger} from "./logger";
const semverInc = require('semver/functions/inc')


export function getLatestversionDate(npmViewResult: NPMViewResult, latestVersion : string){
        const time: StringMap<string> | undefined = npmViewResult['time'];
        const latestVersionDate = time && time[latestVersion];
        return latestVersionDate;
}

export function getNextversionDate(npmViewResult: NPMViewResult, nextVersion: string) {
        const time: StringMap<string> | undefined = npmViewResult['time'];
        const nextVersionDate = time && time[nextVersion];
        return nextVersionDate;

}

export function compareDate(latestDate:string, nextDate:string){
    if (latestDate >= nextDate) {
        return true;
    } {
        return false;
    }
}
