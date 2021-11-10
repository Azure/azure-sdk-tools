/* eslint-disable @typescript-eslint/no-var-requires */
import * as assert from 'assert'
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
