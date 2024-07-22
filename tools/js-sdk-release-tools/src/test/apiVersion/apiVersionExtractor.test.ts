import { expect, test } from "vitest";
import { findApiVersionInRestClient } from "../../mlc/apiVersion/apiVersionTypeExtractor";
import { join } from "path";

test("MLC api-version Extractor: new createClient function", async () => {
    const clientPath = join(__dirname, 'testCases/newClient.ts');
    const version = findApiVersionInRestClient(clientPath);
    expect(version).toBe('2024-03-01-preview');
});

test("MLC api-version Extractor: old createClient function 1", async () => {
    const clientPath = join(__dirname, 'testCases/oldClient1.ts');
    const version = findApiVersionInRestClient(clientPath);
    expect(version).toBe('v1.1-preview.1');
});

test("MLC api-version Extractor: old createClient function 2", async () => {
    const clientPath = join(__dirname, 'testCases/oldClient2.ts');
    const version = findApiVersionInRestClient(clientPath);
    expect(version).toBe('2024-03-01-preview');
});
