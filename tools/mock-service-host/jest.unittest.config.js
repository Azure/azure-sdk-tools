// @ts-check

const mainConfig = require('./jest.config')

module.exports = {
    ...mainConfig,
    testMatch: ['**/test/unittest/**/*.ts', '!**/.history/**']
}
