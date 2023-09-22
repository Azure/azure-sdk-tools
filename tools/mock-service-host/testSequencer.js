const Sequencer = require('@jest/test-sequencer').default

function isIntegrationTest(t) {
    return t.path.indexOf('integrationtest') >= 0
}
class CustomSequencer extends Sequencer {
    sort(tests) {
        // Test structure information
        const copyTests = Array.from(tests)
        return copyTests.sort((testA, testB) => {
            if (isIntegrationTest(testA) && !isIntegrationTest(testB)) return 1
            if (isIntegrationTest(testB) && !isIntegrationTest(testA)) return -1
            return testA.path > testB.path ? 1 : -1
        })
    }
}

module.exports = CustomSequencer
