import { describe, test, expect } from 'vitest';
import { updateApiRefLink } from '../../common/changelog/modifyChangelogFileAndBumpVersion.js';

describe('updateApiRefLink', () => {
    test('should remove ?view=azure-node-preview from apiRefLink when version is stable', () => {
        const packageJsonContent = JSON.stringify({
            name: '@azure/arm-test',
            version: '1.0.0-beta.1',
            'sdk-type': 'mgmt',
            'api-surface': {
                apiRefLink: 'https://docs.microsoft.com/javascript/api/@azure/arm-test?view=azure-node-preview'
            }
        }, null, 2);

        const result = updateApiRefLink(packageJsonContent, '1.0.0');
        expect(result).toContain('"apiRefLink": "https://docs.microsoft.com/javascript/api/@azure/arm-test"');
        expect(result).not.toContain('?view=azure-node-preview');
    });

    test('should keep ?view=azure-node-preview in apiRefLink when version is beta', () => {
        const packageJsonContent = JSON.stringify({
            name: '@azure/arm-test',
            version: '1.0.0',
            'sdk-type': 'mgmt',
            'api-surface': {
                apiRefLink: 'https://docs.microsoft.com/javascript/api/@azure/arm-test'
            }
        }, null, 2);

        const result = updateApiRefLink(packageJsonContent, '1.0.0-beta.1');
        expect(result).toContain('"apiRefLink": "https://docs.microsoft.com/javascript/api/@azure/arm-test?view=azure-node-preview"');
    });

    test('should add ?view=azure-node-preview to apiRefLink when bumping to beta version', () => {
        const packageJsonContent = JSON.stringify({
            name: '@azure/arm-test',
            version: '1.0.0',
            'sdk-type': 'mgmt',
            'api-surface': {
                apiRefLink: 'https://docs.microsoft.com/javascript/api/@azure/arm-test'
            }
        }, null, 2);

        const result = updateApiRefLink(packageJsonContent, '2.0.0-beta.1');
        expect(result).toContain('"apiRefLink": "https://docs.microsoft.com/javascript/api/@azure/arm-test?view=azure-node-preview"');
    });

    test('should not duplicate ?view=azure-node-preview when version is already beta with preview link', () => {
        const packageJsonContent = JSON.stringify({
            name: '@azure/arm-test',
            version: '1.0.0-beta.1',
            'sdk-type': 'mgmt',
            'api-surface': {
                apiRefLink: 'https://docs.microsoft.com/javascript/api/@azure/arm-test?view=azure-node-preview'
            }
        }, null, 2);

        const result = updateApiRefLink(packageJsonContent, '1.0.0-beta.2');
        const occurrences = (result.match(/\?view=azure-node-preview/g) || []).length;
        expect(occurrences).toBe(1);
        expect(result).toContain('"apiRefLink": "https://docs.microsoft.com/javascript/api/@azure/arm-test?view=azure-node-preview"');
    });

    test('should handle package.json without apiRefLink', () => {
        const packageJsonContent = JSON.stringify({
            name: '@azure/arm-test',
            version: '1.0.0-beta.1',
            'sdk-type': 'mgmt'
        }, null, 2);

        const result = updateApiRefLink(packageJsonContent, '1.0.0');
        expect(result).not.toContain('apiRefLink');
        expect(result).not.toContain('?view=azure-node-preview');
    });

    test('should handle package.json with learn.microsoft.com apiRefLink domain', () => {
        const packageJsonContent = JSON.stringify({
            name: '@azure-rest/arm-test',
            version: '1.0.0-beta.1',
            'sdk-type': 'mgmt',
            'api-surface': {
                apiRefLink: 'https://learn.microsoft.com/javascript/api/@azure-rest/arm-test?view=azure-node-preview'
            }
        }, null, 2);

        const result = updateApiRefLink(packageJsonContent, '1.0.0');
        expect(result).toContain('"apiRefLink": "https://learn.microsoft.com/javascript/api/@azure-rest/arm-test"');
        expect(result).not.toContain('?view=azure-node-preview');
    });
});
