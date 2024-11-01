module.exports = {
  preset: 'ts-jest',
  globals: {
    'ts-jest': {
      packageJson: 'package.json',
      diagnostics: false,
      tsConfig: "tsconfig.integration.json"
    },
  },
  testMatch: [
    "<rootDir>/integrationTest/**/*.spec.ts",
  ],
  testPathIgnorePatterns: [
    "<rootDir>/node_modules", "<rootDir>/w", "<rootDir>/test"
  ],
  modulePathIgnorePatterns: [
    "<rootDir>/node_modules", "<rootDir>/w", "<rootDir>/test"
  ],
  unmockedModulePathPatterns: [
    "@microsoft.azure"
  ],
  transform: {
    "^.+\\.(ts|tsx)$": "ts-jest"
  },
  transformIgnorePatterns: [
    "<rootDir>/test", "<rootDir>/w"
  ],
  setupFilesAfterEnv: [
    '<rootDir>/integrationTest/jest.setupAfterEnv.js'
  ],
  moduleFileExtensions: ["ts", "tsx", "js", "jsx", "json", "node"]
}
