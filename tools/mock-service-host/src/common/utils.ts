import * as express from 'express'
import * as lodash from 'lodash'
import * as winston from 'winston'

const myFormat = winston.format.printf(({ level, message, timestamp }) => {
    if (typeof message === 'object' && !isNullOrUndefined(message)) {
        message = JSON.stringify(message, null, 4)
    }
    return `${timestamp} [${level}]: ${message}`
})

export const logger = winston.createLogger({
    level: 'info',
    format: winston.format.combine(winston.format.timestamp(), myFormat)
})

if (process.env.NODE_ENV !== 'production') {
    logger.add(new winston.transports.Console({}))
} else {
    logger.add(new winston.transports.File({ filename: 'log/error.log', level: 'error' }))
    logger.add(new winston.transports.File({ filename: 'log/combined.log' }))
}

export function isNullOrUndefined(obj: any) {
    return obj === null || obj === undefined
}

export function replacePropertyValue(
    property: string,
    newVal: any,
    object: any,
    // eslint-disable-next-line @typescript-eslint/no-unused-vars
    where = (v: any) => {
        return true
    },
    recursively = true
): any {
    const newObject = lodash.clone(object)

    lodash.each(object, (val, key) => {
        if (key === property && where(val)) {
            newObject[key] = newVal
        } else if (typeof val === 'object' && recursively) {
            newObject[key] = replacePropertyValue(property, newVal, val, where)
        }
    })

    return newObject
}

export function removeNullValueKey(object: any): any {
    if (typeof object !== 'object') return object

    const newObject = lodash.clone(object)

    lodash.each(object, (val, key) => {
        if (val === null) {
            delete newObject[key]
        } else if (typeof val === 'object') {
            newObject[key] = removeNullValueKey(val)
        }
    })

    return newObject
}

export function getPureUrl(url: string): string {
    return url?.split('?')[0]
}

export function getPath(pureUrl: string) {
    return pureUrl.split('/').slice(1)
}

export function isObject(item: any) {
    return item && typeof item === 'object' && !Array.isArray(item)
}

export function mergeDeep(target: any, ...sources: any[]): any {
    if (!sources.length) return target
    const source = sources.shift()

    if (isObject(target) && isObject(source)) {
        for (const key in source) {
            if (isObject(source[key])) {
                if (!target[key]) Object.assign(target, { [key]: {} })
                mergeDeep(target[key], source[key])
            } else {
                Object.assign(target, { [key]: source[key] })
            }
        }
    }

    return mergeDeep(target, ...sources)
}

export function setContentTypeAsJson(res: express.Response): express.Response {
    res.setHeader('content-type', 'application/json; charset=utf-8')
    return res
}

// For Azure urls:
//     /subscriptions/randomSub/resourceGroups/randomRG/providers/Microsoft.ApiManagement/service/randomService/users/randomUsers,
//     /subscriptions/randomSub/providers/Microsoft.ApiManagement/service/randomService/users/randomUsers
//     /subscriptions/randomSub/providers/Microsoft.ApiManagement/service/randomService/users/randomUsers/providers/Microsoft.Network/subnets/randomNet
//     /subscriptions/randomSub/resourceGroups/randomRG
// the words 'randomRG', 'randomService', 'randomUsers', 'randomNet' are though as management level word,
// since they are resource names can be created/deleted by the management client.
export function isManagementUrlLevel(level: number, fullUrl: string): boolean {
    const providers = 'providers'
    if (level % 2 === 1 || level === 2) return false

    const mgmtUrlRegex = /\/subscriptions\/(?<subscription>[^/]+)(\/resourceGroups\/(?<resourceGroup>[^/]+))?(\/providers\/(?<namespace>[^/]+))?/i
    const matchObj = mgmtUrlRegex.exec(fullUrl)

    if (!matchObj?.groups?.subscription) {
        // return false since not a management url
        return false
    }
    if (!matchObj?.groups?.namespace && (level !== 4 || !matchObj?.groups?.resourceGroup)) {
        // if no providers defined, the only managment level is the 4 in '/subscriptions/xx/resourceGroups/yy'
        return false
    }

    const path = getPath(fullUrl.toLowerCase())
    if (path.length < level) {
        return false
    }
    if (path[level - 2] === providers) {
        // return false if it's a provider field
        return false
    }

    // return false if the level is after secondary provider Microsoft.Resources
    let foundPrimaryProvider = false
    const resourcesProvider = 'microsoft.resources'
    for (let i = 1; i < Math.min(path.length, level); i += 2) {
        if (!foundPrimaryProvider) {
            if (path[i - 1] === providers) {
                foundPrimaryProvider = true
            }
            continue
        }
        if (path[i - 1] === providers && path[i] === resourcesProvider) {
            return false
        }
    }

    return true
}

export function queryToObject(httpQuery: string): Record<string, string> {
    const ret: Record<string, string> = {}
    for (const param of httpQuery.split('&')) {
        const [k, v] = param.split('=')
        ret[k] = v
    }
    return ret
}

export function setStringIfExist(obj: any, propertyName: string, value: string) {
    if (isNullOrUndefined(obj)) {
        return
    }
    if (
        Object.prototype.hasOwnProperty.call(obj, propertyName) &&
        typeof obj[propertyName] === 'string'
    ) {
        obj[propertyName] = value
    }
}
