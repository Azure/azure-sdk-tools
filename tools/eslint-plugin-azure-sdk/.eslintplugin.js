// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

/**
 * This module exists only for the purpose of exposing this plugin to
 * its own linter. A dependency in node_modules/eslint-plugin-local
 * requires this file and exposes it as a module to the local eslint.
 * 
 * If the package is not built, it will fail to load.
 */

 module.exports = require("./dist");