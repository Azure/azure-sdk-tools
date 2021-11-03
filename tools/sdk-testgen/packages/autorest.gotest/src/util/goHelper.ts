import * as _ from 'lodash';
import { Helper } from '@autorest/testmodeler/dist/src/util/helper';

export class GoHelper {
    public static obejctToString(rawValue: any) {
        let ret = `map[string]interface{}{\n`;
        for (const [key, value] of Object.entries(rawValue)) {
            if (_.isArray(value)) {
                ret += `"${key}":`;
                ret += this.arrayToString(value);
                ret += `,\n`;
            } else if (_.isObject(value)) {
                ret += `"${key}":`;
                ret += this.obejctToString(value);
                ret += `,\n`;
            } else if (_.isString(value)) {
                ret += `"${key}": ${Helper.quotedEscapeString(value)},\n`;
            } else {
                ret += `"${key}": ${value},\n`;
            }
        }
        ret += '}';
        return ret;
    }

    public static arrayToString(rawValue: any) {
        let ret = `[]interface{}{\n`;
        for (const item of rawValue) {
            if (_.isArray(item)) {
                ret += this.arrayToString(item);
                `,\n`;
            } else if (_.isObject(item)) {
                ret += this.obejctToString(item);
                ret += `,\n`;
            } else if (_.isString(item)) {
                ret += `${Helper.quotedEscapeString(item)},\n`;
            } else {
                ret += `${item},\n`;
            }
        }
        ret += '}';
        return ret;
    }

    public static isPrimitiveType(type: string) {
        const firstChar = type[0];
        return firstChar === firstChar.toLowerCase();
    }
}
