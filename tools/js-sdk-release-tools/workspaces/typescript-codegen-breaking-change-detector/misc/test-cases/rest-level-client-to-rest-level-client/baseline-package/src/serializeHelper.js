"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
exports.buildMultiCollection = buildMultiCollection;
function buildMultiCollection(items, parameterName) {
    return items
        .map((item, index) => {
        if (index === 0) {
            return item;
        }
        return `${parameterName}=${item}`;
    })
        .join("&");
}
//# sourceMappingURL=serializeHelper.js.map