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
}
