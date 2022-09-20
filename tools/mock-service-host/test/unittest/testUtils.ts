/* eslint-disable @typescript-eslint/no-var-requires */
import * as assert from 'assert'
import { isManagementUrlLevel } from '../../src/common/utils'

describe('isManagementUrlLevel() :', () => {
    it('For normal urls:', async () => {
        const urlWithRG =
            '/subscriptions/randomSub/resourceGroups/randomRG/providers/Microsoft.ApiManagement/service/randomService/users/randomUsers'
        const urlWithoutRG =
            '/subscriptions/randomSub/providers/Microsoft.ApiManagement/service/randomService/users/randomUsers'
        const urlRG = '/subscriptions/randomSub/resourceGroups/randomRG'
        const urlWithSubprovider =
            '/subscriptions/randomSub/providers/Microsoft.ApiManagement/service/randomService/users/randomUsers/providers/Microsoft.Network/subnets/randomNet'
        const urlWithSubproviderResource =
            '/subscriptions/randomSub/providers/Microsoft.ApiManagement/service/randomService/users/randomUsers/providers/Microsoft.Resources/tags/anytag/type2/anything'
        assert.ok(isManagementUrlLevel(4, urlWithRG))
        assert.ok(isManagementUrlLevel(8, urlWithRG))
        assert.ok(isManagementUrlLevel(10, urlWithRG))
        assert.ok(isManagementUrlLevel(6, urlWithoutRG))
        assert.ok(isManagementUrlLevel(8, urlWithoutRG))
        assert.ok(isManagementUrlLevel(4, urlRG))
        assert.ok(isManagementUrlLevel(12, urlWithSubprovider))
        assert.ok(isManagementUrlLevel(6, urlWithSubproviderResource))
        assert.ok(isManagementUrlLevel(8, urlWithSubproviderResource))

        assert.strictEqual(isManagementUrlLevel(1, urlWithRG), false)
        assert.strictEqual(isManagementUrlLevel(2, urlWithRG), false)
        assert.strictEqual(isManagementUrlLevel(6, urlWithRG), false)
        assert.strictEqual(isManagementUrlLevel(7, urlWithRG), false)
        assert.strictEqual(isManagementUrlLevel(1, urlWithoutRG), false)
        assert.strictEqual(isManagementUrlLevel(2, urlWithoutRG), false)
        assert.strictEqual(isManagementUrlLevel(4, urlWithoutRG), false)
        assert.strictEqual(isManagementUrlLevel(5, urlWithoutRG), false)
        assert.strictEqual(isManagementUrlLevel(7, urlWithoutRG), false)
        assert.strictEqual(isManagementUrlLevel(2, urlRG), false)
        assert.strictEqual(isManagementUrlLevel(10, urlWithSubprovider), false)
        assert.strictEqual(isManagementUrlLevel(10, urlWithSubproviderResource), false)
        assert.strictEqual(isManagementUrlLevel(11, urlWithSubproviderResource), false)
        assert.strictEqual(isManagementUrlLevel(12, urlWithSubproviderResource), false)
        assert.strictEqual(isManagementUrlLevel(13, urlWithSubproviderResource), false)
        assert.strictEqual(isManagementUrlLevel(14, urlWithSubproviderResource), false)
    })

    it('return false for non management urls:', async () => {
        assert.strictEqual(
            isManagementUrlLevel(
                8,
                '/subscriptions/randomSub/resourceGroups/randomRG/fakesA/fake/fakesB/fake'
            ),
            false
        )

        assert.strictEqual(
            isManagementUrlLevel(
                8,
                '/subscriptions/randomSub/resourceGroupsBBB/randomRG/providers/Microsoft.ApiManagement/fakesA/fake/fakesB/fake'
            ),
            false
        )

        assert.strictEqual(
            isManagementUrlLevel(6, '/subscriptions/randomSub/fakesA/fake/fakesB/fake'),
            false
        )

        assert.strictEqual(
            isManagementUrlLevel(
                6,
                '/subscriptionsAAA/randomSub/providers/Microsoft.ApiManagement/service/randomService/users/randomUsers'
            ),
            false
        )
    })
})
