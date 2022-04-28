/** @type {import('ts-jest/dist/types').InitialOptionsTsJest} */
module.exports = {
    preset: 'ts-jest',
    testEnvironment: 'node',
    globals: {
        "ts-jest": {
            packageJson: "package.json",
            tsConfig: "tsconfig.json"
        }
    },
    testMatch: [
        "<rootDir>/**/*.test.ts",
    ],
};