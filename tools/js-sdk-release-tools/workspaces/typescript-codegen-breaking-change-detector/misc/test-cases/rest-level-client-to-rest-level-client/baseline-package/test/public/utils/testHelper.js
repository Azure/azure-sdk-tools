"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
Object.defineProperty(exports, "__esModule", { value: true });
exports.editDistance = editDistance;
exports.distance = distance;
function editDistance(s1, s2) {
    const n1 = s1.length;
    const n2 = s2.length;
    return distance(s1, s2, n1, n2);
}
function distance(s1, s2, n1, n2) {
    if (n1 === 0) {
        return n2;
    }
    if (n2 === 0) {
        return n1;
    }
    if (s1[n1 - 1] === s2[n2 - 1]) {
        const d = distance(s1, s2, n1 - 1, n2 - 1);
        return d;
    }
    const nums = [
        distance(s1, s2, n1, n2 - 1),
        distance(s1, s2, n1 - 1, n2),
        distance(s1, s2, n1 - 1, n2 - 1),
    ];
    return 1 + Math.min(...nums);
}
//# sourceMappingURL=testHelper.js.map