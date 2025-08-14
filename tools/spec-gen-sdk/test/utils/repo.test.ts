import { describe, it, expect } from 'vitest';
import { getRepository, getRepoKey, repoKeyToString, Repository, RepoKey } from '../../src/utils/repo';

describe('repo utils', () => {
    describe('getRepository', () => {
        it('should parse valid GitHub URL', () => {
            expect(getRepository('https://github.com/azure/azure-rest-api-specs')).toEqual({
                owner: 'azure',
                name: 'azure-rest-api-specs'
            });
            expect(getRepository('')).toEqual({
                owner: '',
                name: ''
            });
        });

        it('should convert URL to lowercase', () => {
            const result = getRepository('https://github.com/Azure/Azure-Rest-API-Specs');
            expect(result).toEqual({
                owner: 'azure',
                name: 'azure-rest-api-specs' 
            });
        });

        it('should throw error for invalid URL format', () => {
            expect(() => getRepository('invalid-url')).toThrow();
            expect(() => getRepository('https://not-github.com/owner/repo')).toThrow();
        });
    });

    describe('getRepoKey', () => {
        it('should handle null/undefined input', () => {
            const result = getRepoKey('');
            expect(result).toEqual({
                owner: '',
                name: ''
            });
        });

        it('should parse string with forward slash', () => {
            const result = getRepoKey('azure/sdk-repo');
            expect(result).toEqual({
                owner: 'azure',
                name: 'sdk-repo'
            });
        });

        it('should parse string with backslash', () => {
            const result = getRepoKey('azure\\sdk-repo');
            expect(result).toEqual({
                owner: 'azure',
                name: 'sdk-repo'
            });
        });

        it('should handle string without separator', () => {
            const result = getRepoKey('repo-name');
            expect(result).toEqual({
                owner: '',
                name: 'repo-name'
            });
        });

        it('should return RepoKey object as-is', () => {
            const input: RepoKey = {
                owner: 'test-owner',
                name: 'test-repo'
            };
            const result = getRepoKey(input);
            expect(result).toEqual(input);
        });
    });

    describe('repoKeyToString', () => {
        it('should convert RepoKey to string format', () => {
            const input: RepoKey = {
                owner: 'azure',
                name: 'sdk-repo'
            };
            const result = repoKeyToString(input);
            expect(result).toBe('azure/sdk-repo');
        });

        it('should handle RepoKey with empty owner', () => {
            const input: RepoKey = {
                owner: '',
                name: 'repo-name'
            };
            const result = repoKeyToString(input);
            expect(result).toBe('/repo-name');
        });
    });
});