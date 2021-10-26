module.exports = {
    transform: {
        '^.+\\.tsx?$': 'ts-jest',
    },
    testEnvironment: 'node',
    moduleFileExtensions: ['ts', 'js', 'json', 'node'],
    moduleNameMapper: {},
    collectCoverage: true,
    collectCoverageFrom: ['./src/**/*.ts', '!**/node_modules/**'],
    coverageReporters: ['json', 'lcov', 'cobertura', 'text', 'html', 'clover'],
    coveragePathIgnorePatterns: ['/node_modules/', '.*/tests/.*'],
    globals: {
        'ts-jest': {
            tsconfig: 'tsconfig.json',
        },
    },
    testMatch: ['test/**/*.ts', '**/test/**/*.ts', '!**/test/**/*.d.ts', '!**/test/**/tools.ts'],
    verbose: true,
    testTimeout: 300000,
};
