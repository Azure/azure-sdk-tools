import 'reflect-metadata' // Must be imported exactly once for Inversify
/* eslint-disable @typescript-eslint/no-var-requires */
import * as assert from 'assert'
import * as path from 'path'
import { JsonLoader } from 'oav/dist/lib/swagger/jsonLoader'
import { MockerCache, PayloadCache } from 'oav/dist/lib/generator/exampleCache'
import { ResponseGenerator, SpecItem } from '../../src/mid/responser'
import { SwaggerSpec } from 'oav/dist/lib/swagger/swaggerTypes'
import { config } from '../../src/common/index'
import { inversifyGetInstance } from 'oav/dist/lib/inversifyUtils'
import Mocker from '../../src/mid/oav/mocker'
import SwaggerMocker from '../../src/mid/oav/swaggerMocker'

describe('isManagementUrlLevel() :', () => {
    it('patch for userassignedidentities', async () => {
        const result = SwaggerMocker.mockUserAssignedIdentities(
            {
                mockAny: {
                    userAssignedIdentities: {
                        oldKey: {
                            clientId: 'oldClientId',
                            principalId: 'oldClientId'
                        }
                    }
                }
            },
            { subscriptions: 'mock' },
            false
        )

        assert.ok(
            Object.keys(result.mockAny.userAssignedIdentities).every((x) =>
                x.startsWith('/subscriptions/mock/')
            )
        )
        assert.strictEqual(Object.keys(result.mockAny.userAssignedIdentities).length, 1)
    })

    it('flattenPath', async () => {
        const result = SwaggerMocker.flattenPath(
            '/subscriptions/aaa/resourceGroups/bbb/anyType/anyValue'
        )

        assert.strictEqual(
            JSON.stringify(result),
            JSON.stringify({
                subscriptions: 'aaa',
                resourcegroups: 'bbb',
                anytype: 'anyValue'
            })
        )
    })
})

describe('Mocker: ', () => {
    it('mock base64url', async () => {
        const mocker = new Mocker()
        const result = mocker.mock(
            {
                type: 'string',
                format: 'base64url'
            },
            'fakeName'
        )

        assert.strictEqual(Buffer.from(result, 'base64url').toString('base64url'), result)
    })
})

describe('mockForExample: ', () => {
    it('mock resourceType', async () => {
        const jsonLoader = inversifyGetInstance(JsonLoader, {})
        const swaggerMocker = new SwaggerMocker(jsonLoader, new MockerCache(), new PayloadCache())

        const specFile = path.join(
            path.resolve(config.specRetrievalLocalRelativePath),
            'specification',
            'apimanagement',
            'resource-manager',
            'Microsoft.ApiManagement',
            'preview',
            '2018-01-01',
            'apimanagement.json'
        )
        const spec = (await (jsonLoader.load(specFile) as unknown)) as SwaggerSpec
        const specItem = ResponseGenerator.getSpecItem(spec, 'Policy_CreateOrUpdate')
        expect(specItem).not.toEqual(undefined)

        const example: Record<string, any> = {
            parameters: {},
            responses: {}
        }
        example.responses['200'] = swaggerMocker.mockEachResponse('200', {}, specItem as SpecItem)
        swaggerMocker.patchExampleResponses(example, {
            method: 'put',
            url:
                'https://localhost:8443/subscriptions/xxx/resourceGroups/yy/providers/Microsoft.ApiManagement/service/serviceName/policies/policyId'
        })

        assert.strictEqual(example.responses['200'].body.test, undefined)

        expect({
            responseResourceType: example.responses['200'].body.type,
            responseResourceId: example.responses['200'].body.id
        }).toMatchSnapshot()
    })

    it('patchResourceIdAndType', async () => {
        const jsonLoader = inversifyGetInstance(JsonLoader, {})
        const swaggerMocker = new SwaggerMocker(jsonLoader, new MockerCache(), new PayloadCache())
        let responses: any = {
            200: {
                body: {
                    id: 'WrongId',
                    type: 'WrongType'
                }
            }
        }

        // put with normal providers
        swaggerMocker.patchResourceIdAndType(responses, {
            method: 'put',
            url:
                'https://localhost:8443/subscriptions/xxx/resourceGroups/yy/providers/Microsoft.ApiManagement/service/serviceName/policies/policyId'
        })
        expect(responses).toMatchSnapshot()

        // list with nested providers
        responses['200']['body'] = {
            id: 'WrongId',
            type: 'WrongType'
        }
        swaggerMocker.patchResourceIdAndType(responses, {
            method: 'get',
            url:
                'https://localhost:8443/subscriptions/xxx/resourceGroups/yy/providers/Microsoft.ApiManagement/service/serviceName/providers/Microsoft.MokeNested/foos/myfoo/bars/mybar'
        })
        expect(responses).toMatchSnapshot()

        // list with nested providers
        responses = {
            200: {
                body: [
                    {
                        id: 'WrongId',
                        type: 'WrongType'
                    }
                ]
            }
        }
        swaggerMocker.patchResourceIdAndType(responses, {
            method: 'get',
            url:
                'https://localhost:8443/subscriptions/xxx/resourceGroups/yy/providers/Microsoft.ApiManagement/service/serviceName/providers/Microsoft.MokeNested/foos/myfoo/bars'
        })
        expect(responses).toMatchSnapshot()
    })
})
