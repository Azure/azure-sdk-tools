/** @type {import('ts-jest/dist/types').InitialOptionsTsJest} */
module.exports = {
    verbose: true,
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
    modulePathIgnorePatterns: [
        "<rootDir>(/.*)*/tmp/*"
    ],
    collectCoverage: true,
    collectCoverageFrom: [
        "src/**/*.ts"
    ],
    coveragePathIgnorePatterns: [
        "src/cli/pipelineCli",
        "src/cli/dockerCli/*Cli.ts"
    ],
    roots: [
        "<rootDir>/src/",
        "<rootDir>/test/"
    ],
    transform: {
        "^.+\\.tsx?$": "ts-jest"
    },
    moduleFileExtensions: [
        "ts",
        "tsx",
        "js",
        "jsx",
        "json",
        "node"
    ],
    testPathIgnorePatterns: [
        "__snapshots__"
    ]
};