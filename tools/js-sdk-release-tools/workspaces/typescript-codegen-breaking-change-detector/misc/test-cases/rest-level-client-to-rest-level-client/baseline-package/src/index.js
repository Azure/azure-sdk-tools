"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
const tslib_1 = require("tslib");
const customClient_1 = tslib_1.__importDefault(require("./custom/customClient"));
tslib_1.__exportStar(require("./custom/customClient"), exports);
tslib_1.__exportStar(require("./parameters"), exports);
tslib_1.__exportStar(require("./responses"), exports);
tslib_1.__exportStar(require("./clientDefinitions"), exports);
tslib_1.__exportStar(require("./isUnexpected"), exports);
tslib_1.__exportStar(require("./models"), exports);
tslib_1.__exportStar(require("./outputModels"), exports);
tslib_1.__exportStar(require("./serializeHelper"), exports);
exports.default = customClient_1.default;
//# sourceMappingURL=index.js.map