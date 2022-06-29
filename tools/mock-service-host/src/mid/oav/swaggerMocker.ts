import * as util from 'oav/dist/lib/generator/util'
import {
    CacheItem,
    MockerCache,
    PayloadCache,
    buildItemOption,
    createLeafItem,
    createTrunkItem,
    reBuildExample
} from 'oav/dist/lib/generator/exampleCache'
import { ExampleRule, getRuleValidator } from 'oav/dist/lib/generator/exampleRule'
import { JsonLoader } from 'oav/dist/lib/swagger/jsonLoader'
import { LiveRequest } from 'oav/dist/lib/liveValidation/operationValidator'
import { isNullOrUndefined, logger, setStringIfExist } from '../../common/utils'
import { mockedResourceType } from '../../common/constants'
import { parse as parseUrl } from 'url'
import { xmsAzureResource } from 'oav/dist/lib/util/constants'
import Mocker from './mocker'

export default class SwaggerMocker {
    private jsonLoader: JsonLoader
    private mocker: Mocker
    private spec: any
    private mockCache: MockerCache
    private exampleCache: PayloadCache
    private exampleRule?: ExampleRule

    public constructor(
        jsonLoader: JsonLoader,
        mockerCache: MockerCache,
        payloadCache: PayloadCache
    ) {
        this.jsonLoader = jsonLoader
        this.mocker = new Mocker()
        this.mockCache = mockerCache
        this.exampleCache = payloadCache
    }

    public setRule(exampleRule?: ExampleRule) {
        this.exampleRule = exampleRule
    }

    public patchExampleResponses(example: any, liveRequest: LiveRequest) {
        this.patchResourceIdAndType(example.responses, liveRequest)
        this.patchUserAssignedIdentities(example.responses, liveRequest)
    }

    private isValidId(id: string): boolean {
        if (isNullOrUndefined(id) || id.indexOf(mockedResourceType) >= 0) return false
        // is valid id if start with '/' and there is no special chars
        const segments = id.split('/')
        const guidPattern = new RegExp(
            '/^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i'
        )
        if (
            segments.length < 3 ||
            (segments[1].toLowerCase() === 'subscriptions' && !guidPattern.test(segments[2]))
        )
            return false
        return id.match(/^\/.+/) !== null && id.match(/[{}[]()]+/) === null
    }

    private isValidType(t: string): boolean {
        if (isNullOrUndefined(t) || t === mockedResourceType) return false
        // is valid type if there is '.' and there is no special chars
        return t.match(/\./) !== null && t.match(/[{}[]()]+/) === null
    }

    /**
     * Replaces mock resource IDs with IDs that match current resource.
     */
    public patchResourceIdAndType(responses: any, liveRequest: LiveRequest) {
        const url = parseUrl(liveRequest.url)

        const pathElements = (url.pathname || '').split('/')
        let resourceType = ''
        let providerIdx = pathElements.length - 2
        for (; providerIdx > 0; providerIdx--) {
            if (
                providerIdx % 2 === 1 &&
                pathElements[providerIdx].match(/providers/i) &&
                pathElements[providerIdx + 1].match(/microsoft\..+/i)
            ) {
                resourceType = pathElements[providerIdx + 1]
                break
            }
        }
        if (providerIdx > 0) {
            for (let i = providerIdx + 2; i < pathElements.length; i += 2) {
                resourceType = `${resourceType}/${pathElements[i]}`
            }
        } else {
            resourceType = mockedResourceType
        }

        Object.keys(responses).forEach((key) => {
            if (responses[key]?.body?.id && !this.isValidId(responses[key].body.id)) {
                // put
                if (liveRequest.method.toLowerCase() === 'put') {
                    responses[key].body.id = url.pathname
                }
                // get(get) or patch
                if (
                    liveRequest.method.toLowerCase() === 'get' ||
                    liveRequest.method.toLowerCase() === 'patch'
                ) {
                    responses[key].body.id = url.pathname
                }
            }
            if (!this.isValidType(responses[key]?.body?.type))
                setStringIfExist(responses[key]?.body, 'type', resourceType)

            // get(list)
            for (const arr of [
                responses[key]?.body?.value /*pagable list*/,
                responses[key]?.body /*non-pagable list*/
            ]) {
                if (Array.isArray(arr) && arr.length) {
                    arr.forEach((item: any) => {
                        if (item.id && !this.isValidId(item.id)) {
                            const resourceName = item.name || 'resourceName'
                            item.id = `${url.pathname}/${resourceName}`
                        }
                        if (!this.isValidType(item.type))
                            setStringIfExist(item, 'type', resourceType)
                    })
                }
            }
        })
    }

    public static flattenPath(path: string): Record<string, string> {
        const items = path.split('/')
        const ret: Record<string, string> = {}
        for (let i = 2; i < items.length; i += 2) {
            if (items[i].length > 0) ret[items[i - 1].toLowerCase()] = items[i]
        }
        return ret
    }

    public static mockUserAssignedIdentities(
        obj: any,
        pathElements: Record<string, string>,
        inUserAssignedIdentities = false
    ): any {
        if (isNullOrUndefined(obj)) return obj
        if (Array.isArray(obj)) {
            return obj.map((x) => this.mockUserAssignedIdentities(x, pathElements))
        } else if (typeof obj === 'object') {
            const ret: Record<string, any> = {}
            // eslint-disable-next-line prefer-const
            for (let [key, item] of Object.entries(obj)) {
                if (
                    inUserAssignedIdentities &&
                    !key.match(
                        /\/subscriptions\/.*\/providers\/Microsoft.ManagedIdentity\/userAssignedIdentities\/.*/i
                    )
                ) {
                    const subscription =
                        pathElements.subscriptions || '00000000-0000-0000-0000-000000000000'
                    const resourceGroup = pathElements.subscriptions || 'mockGroup'
                    key = `/subscriptions/${subscription}/resourceGroups/${resourceGroup}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/mocked`
                }
                ret[key] = this.mockUserAssignedIdentities(
                    item,
                    pathElements,
                    key.toLowerCase() === 'userassignedidentities'
                )
            }
            return ret
        }
        return obj
    }

    private patchUserAssignedIdentities(responses: any, liveRequest: LiveRequest) {
        const url = parseUrl(liveRequest.url)
        const pathElements = SwaggerMocker.flattenPath(url.pathname!)
        Object.keys(responses).forEach((key) => {
            if (responses[key]?.body) {
                responses[key].body = SwaggerMocker.mockUserAssignedIdentities(
                    responses[key].body,
                    pathElements,
                    false
                )
            }
        })
    }

    public mockEachResponse(statusCode: string, responseExample: any, specItem: any) {
        const visited = new Set<string>()
        const validator = getRuleValidator(this.exampleRule).onResponseBody
        const responseSpec = specItem.content.responses[statusCode]
        if (validator && !validator({ schema: responseSpec })) {
            return undefined
        }
        return {
            headers: responseExample.hearders || this.mockHeaders(statusCode, specItem),
            body:
                'schema' in responseSpec
                    ? this.mockObj(
                          'response body',
                          responseSpec.schema,
                          responseExample.body || {},
                          visited,
                          false
                      ) || {}
                    : undefined
        }
    }

    private mockHeaders(statusCode: string, specItem: any) {
        if (statusCode !== '201' && statusCode !== '202') {
            return undefined
        }
        const validator = getRuleValidator(this.exampleRule).onResponseHeader
        if (validator && !validator({ schema: specItem })) {
            return undefined
        }
        const headerAttr = util.getPollingAttr(specItem)
        if (!headerAttr) {
            return
        }
        return {
            [headerAttr]: 'LocationURl'
        }
    }

    private mockRequest(paramExample: any, paramSpec: any, rp: string) {
        const validator = getRuleValidator(this.exampleRule).onParameter
        for (const pName of Object.keys(paramSpec)) {
            const element = paramSpec[pName]
            const visited = new Set<string>()

            const paramEle = this.getDefSpec(element, visited)
            if (paramEle.name === 'resourceGroupName') {
                paramExample.resourceGroupName = `rg${rp}`
            } else if (paramEle.name === 'api-version') {
                paramExample['api-version'] = this.spec.info.version
            } else if ('schema' in paramEle) {
                // {
                //     "name": "parameters",
                //     "in": "body",
                //     "required": false,
                //     "schema": {
                //       "$ref": "#/definitions/SignalRResource"
                //     }
                // }
                if (!validator || validator({ schema: paramEle })) {
                    paramExample[paramEle.name] = this.mockObj(
                        paramEle.name,
                        paramEle.schema,
                        paramExample[paramEle.name] || {},
                        visited,
                        true
                    )
                }
            } else {
                if (paramEle.name in paramExample) {
                    continue
                }
                // {
                //     "name": "api-version",
                //     "in": "query",
                //     "required": true,
                //     "type": "string"
                // }
                if (!validator || validator({ schema: paramEle })) {
                    paramExample[paramEle.name] = this.mockObj(
                        paramEle.name,
                        element, // use the original schema  containing "$ref" which will hit the cached value
                        paramExample[paramEle.name],
                        new Set<string>(),
                        true
                    )
                }
            }
        }
        return paramExample
    }

    private removeFromSet(schema: any, visited: Set<string>) {
        if ('$ref' in schema && visited.has(schema.$ref)) {
            visited.delete(schema.$ref)
        }
    }

    private getCache(schema: any) {
        if ('$ref' in schema) {
            for (const cache of [this.exampleCache, this.mockCache]) {
                if (cache.has(schema.$ref.split('#')[1])) {
                    return cache.get(schema.$ref.split('#')[1])
                }
            }
        }
        return undefined
    }

    private isAzureResource(schema: any, visited: Set<string>): boolean {
        const definitionSpec = this.getDefSpec(schema, visited)

        // check by x-ms-azure-resource
        if (definitionSpec[xmsAzureResource]) {
            return true
        }

        // check by property id&type&name
        const allProperties = definitionSpec.properties || {}
        if (
            allProperties.id?.type === 'string' &&
            allProperties.name?.type === 'string' &&
            allProperties.type?.type === 'string'
        ) {
            return true
        }

        // check parents
        for (const parent of definitionSpec.allOf || []) {
            const parentDefinitionSpec = this.getDefSpec(parent, visited)
            if (this.isAzureResource(parentDefinitionSpec, visited)) {
                return true
            }
        }

        return false
    }

    private mockObj(
        objName: string,
        schema: any,
        example: any,
        visited: Set<string>,
        isRequest: boolean
    ) {
        const cache = this.mockCachedObj(objName, schema, example, visited, isRequest)
        const validator = getRuleValidator(this.exampleRule).onSchema
        return reBuildExample(cache, isRequest, schema, validator)
    }

    private mockCachedObj(
        objName: string,
        schema: any,
        example: any,
        visited: Set<string>,
        isRequest: boolean,
        discriminatorValue: string | undefined = undefined,
        useCache = false,
        inAzureResource = false
    ) {
        if (!schema || typeof schema !== 'object') {
            logger.warn(`invalid schema.`)
            return undefined
        }
        // use visited set to avoid circular dependency
        if ('$ref' in schema && visited.has(schema.$ref)) {
            return undefined
        }
        const cache = this.getCache(schema)
        if (useCache && cache) {
            return cache
        }
        const definitionSpec = this.getDefSpec(schema, visited)

        if (util.isObject(definitionSpec)) {
            const isAzureResourceObj = this.isAzureResource(schema, visited)
            // circular inherit will not be handled
            const properties = this.getProperties(definitionSpec, visited)
            example = example || {}
            const discriminator = this.getDiscriminator(definitionSpec, visited)
            if (
                discriminator &&
                !discriminatorValue &&
                properties &&
                Object.keys(properties).includes(discriminator)
            ) {
                return (
                    this.mockForDiscriminator(
                        definitionSpec,
                        example,
                        discriminator,
                        isRequest,
                        visited,
                        isAzureResourceObj
                    ) || undefined
                )
            } else {
                Object.keys(properties).forEach((key: string) => {
                    // the objName is the discriminator when discriminatorValue is specified.
                    if (key === objName && discriminatorValue) {
                        example[key] = createLeafItem(
                            discriminatorValue,
                            buildItemOption(properties[key])
                        )
                    } else {
                        // do not mock response value with x-ms-secret
                        if (isRequest || !properties[key]['x-ms-secret']) {
                            example[key] = this.mockCachedObj(
                                key,
                                properties[key],
                                example[key],
                                visited,
                                isRequest,
                                discriminatorValue,
                                false,
                                isAzureResourceObj
                            )
                        }
                    }
                })
            }
            if ('additionalProperties' in definitionSpec && definitionSpec.additionalProperties) {
                const newKey = util.randomKey()
                if (newKey in properties) {
                    logger.error(`generate additionalProperties for ${objName} fail`)
                } else {
                    example[newKey] = this.mockCachedObj(
                        newKey,
                        definitionSpec.additionalProperties,
                        undefined,
                        visited,
                        isRequest,
                        discriminatorValue,
                        false,
                        inAzureResource
                    )
                }
            }
        } else if (definitionSpec.type === 'array') {
            example = example || []
            const arrItem: any = this.mockCachedObj(
                `${objName}'s item`,
                definitionSpec.items,
                example[0],
                visited,
                isRequest
            )
            example = this.mocker.mock(definitionSpec, objName, arrItem)
        } else {
            /** type === number or integer  */
            example =
                example && typeof example !== 'object'
                    ? example
                    : this.mocker.mock(definitionSpec, objName, undefined, inAzureResource)
        }
        // return value for primary type: string, number, integer, boolean
        // "aaaa"
        // removeFromSet: once we try all roads started from present node, we should remove it and backtrack
        this.removeFromSet(schema, visited)

        let cacheItem: CacheItem
        if (Array.isArray(example)) {
            const cacheChild: CacheItem[] = []
            for (const item of example) {
                cacheChild.push(item)
            }
            cacheItem = createTrunkItem(cacheChild, buildItemOption(definitionSpec))
        } else if (typeof example === 'object') {
            const cacheChild: { [index: string]: CacheItem } = {}
            for (const [key, item] of Object.entries(example)) {
                cacheChild[key] = item as CacheItem
            }
            cacheItem = createTrunkItem(cacheChild, buildItemOption(definitionSpec))
        } else {
            cacheItem = createLeafItem(example, buildItemOption(definitionSpec))
        }
        cacheItem.isMocked = true
        const requiredProperties = this.getRequiredProperties(definitionSpec)
        if (requiredProperties && requiredProperties.length > 0) {
            cacheItem.required = requiredProperties
        }
        if (useCache) {
            this.mockCache.checkAndCache(schema, cacheItem)
        }
        return cacheItem
    }

    /**
     * return all required properties of the object, including parent's properties defined by 'allOf'
     * It will not spread properties' properties.
     * @param definitionSpec
     */
    private getRequiredProperties(definitionSpec: any) {
        let requiredProperties: string[] = Array.isArray(definitionSpec.required)
            ? definitionSpec.required
            : []
        definitionSpec.allOf?.map((item: any) => {
            requiredProperties = [
                ...requiredProperties,
                ...this.getRequiredProperties(this.getDefSpec(item, new Set<string>()))
            ]
        })
        return requiredProperties
    }

    // TODO: handle discriminator without enum options
    private mockForDiscriminator(
        schema: any,
        example: any,
        discriminator: string,
        isRequest: boolean,
        visited: Set<string>,
        inAzureResource: boolean
    ): any {
        const disDetail = this.getDefSpec(schema, visited)
        if (disDetail.discriminatorMap && Object.keys(disDetail.discriminatorMap).length > 0) {
            const properties = this.getProperties(disDetail, new Set<string>())
            let discriminatorValue
            if (properties[discriminator] && Array.isArray(properties[discriminator].enum)) {
                discriminatorValue = properties[discriminator].enum[0]
            } else {
                discriminatorValue = Object.keys(disDetail.discriminatorMap)[0]
            }
            const discriminatorSpec = disDetail.discriminatorMap[discriminatorValue]
            if (!discriminatorSpec) {
                this.removeFromSet(schema, visited)
                return example
            }
            const cacheItem =
                this.mockCachedObj(
                    discriminator,
                    discriminatorSpec,
                    {},
                    new Set<string>(),
                    isRequest,
                    discriminatorValue,
                    inAzureResource
                ) || undefined
            this.removeFromSet(schema, visited)
            return cacheItem
        }
        this.removeFromSet(schema, visited)
        return undefined
    }

    // {
    //  "$ref": "#/parameters/ApiVersionParameter"
    // },
    // to
    // {
    //     "name": "api-version",
    //     "in": "query",
    //     "required": true,
    //     "type": "string"
    // }
    private getDefSpec(schema: any, visited: Set<string>) {
        if ('$ref' in schema) {
            visited.add(schema.$ref)
        }

        const content = this.jsonLoader.resolveRefObj(schema)
        if (!content) {
            return undefined
        }
        return content
    }

    private getProperties(definitionSpec: any, visited: Set<string>) {
        let properties: any = {}
        definitionSpec.allOf?.map((item: any) => {
            properties = {
                ...properties,
                ...this.getProperties(this.getDefSpec(item, visited), visited)
            }
            this.removeFromSet(item, visited)
        })
        return {
            ...properties,
            ...definitionSpec.properties
        }
    }

    private getDiscriminator(definitionSpec: any, visited: Set<string>) {
        let discriminator = undefined
        if (definitionSpec.discriminator) {
            return definitionSpec.discriminator
        }
        definitionSpec.allOf?.some((item: any) => {
            discriminator = this.getDiscriminator(this.getDefSpec(item, visited), visited)
            this.removeFromSet(item, visited)
            return !!discriminator
        })
        return discriminator
    }
}
