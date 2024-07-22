import { expect, test } from "vitest";
import { findApiVersionInRestClientV2 } from "../../mlc/apiVersion/apiVersionTypeExtractor";
import { join } from "path";

test("MLC api-version Extractor: new createClient function", async () => {
    const clientPath = join(__dirname, 'testCases/client.ts');
    const version = findApiVersionInRestClientV2(clientPath);
    expect(version).toBe('2024-03-01-preview');
});
