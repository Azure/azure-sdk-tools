module.exports = {
  rootDir: ".",
  globals: {
    'ts-jest': {
      packageJson: 'package.json',
    },
  },
  transform: {
    "^.+\\.(ts|tsx)$": "ts-jest"
  },
  testMatch: ["**/*.test.ts"],
  testPathIgnorePatterns: ["node_modules"],
  moduleFileExtensions: ["ts", "tsx", "js", "jsx", "json", "node"]
}
