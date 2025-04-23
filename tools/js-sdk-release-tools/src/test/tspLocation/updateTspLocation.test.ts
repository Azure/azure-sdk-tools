import * as fs from "fs";
import * as path from "path";

import { describe, expect, test } from 'vitest';
import { join } from 'path';
import { updateTspLocation } from "../../xlc/codeUpdate/updateTspLocation.js";

describe('Update tsp-location.yaml', () => {
    test('Update tsp-location.yaml', async () => {
        const root = join(__dirname, 'testCases/');
        updateTspLocation(root);
        const data: string = fs.readFileSync(path.join(root, "tsp-location.yaml"), 'utf8');
        expect(data.includes(`Azure/azure-rest-api-specs`)).toBe(true);
    });
});