'use strict';

const build = require('@microsoft/node-library-build');
const gulp = require('gulp');

build.initialize(gulp);

build.lintCmd.enabled = false;

build.preCopy.setConfig({
  shouldFlatten: false,
  copyTo: {
    'lib': ['src/**/*.handlebars']
  }
});

build.jest.setConfig({
  isEnabled: true,
  testMatch: "*.spec.ts",
  testPathIgnorePatterns: ["node_modules", "work"]
});
