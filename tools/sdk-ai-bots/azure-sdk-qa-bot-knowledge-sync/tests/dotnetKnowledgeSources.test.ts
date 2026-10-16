import { afterAll, describe, expect, it } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { ConfigurationLoader } from '../src/services/ConfigurationLoader';
import { processMarkdownFile } from '../src/DailySyncKnowledge';

const configPath = path.join(__dirname, '../config/knowledge-config.json');
const originalPath = ConfigurationLoader.getConfigPath();
const tempDirs: string[] = [];

afterAll(() => {
    ConfigurationLoader.setConfigPath(originalPath);
    for (const dir of tempDirs) {
        fs.rmSync(dir, { recursive: true, force: true });
    }
});

describe('.NET knowledge configuration', () => {
    it('selects the requested sparse-checkout paths and preserves citation roots', () => {
        ConfigurationLoader.setConfigPath(configPath);
        const expectedPaths = [
            '/doc',
            '/sdk/resourcemanager/Azure.ResourceManager/docs',
            '/eng/packages/http-client-csharp-mgmt/docs',
            '/eng/packages/http-client-csharp-mgmt/README.md',
            '/eng/packages/http-client-csharp/.tspd/docs',
            '/eng/packages/http-client-csharp/README.md',
            '/.github/skills/mitigate-breaking-changes',
            '/.github/skills/mgmt-review-comment-resolution',
            '/sdk/core/Azure.Core.TestFramework/README.md',
            '/eng/common/testproxy/onboarding',
            '/eng/common/tsp-client/README.md',
            '/eng/common/knowledge/customizing-client-tsp.md',
        ];
        const configured = ConfigurationLoader.loadConfig().sources
            .find(source => source.repository.url === 'https://github.com/Azure/azure-sdk-for-net.git');
        expect(configured?.paths.map(entry => entry.path)).toEqual(expectedPaths);
        expect(configured?.paths.every(entry =>
            entry.relativeByRepoPath && entry.folder === 'azure_sdk_for_net_docs')).toBe(true);

        const sources = ConfigurationLoader.getDocumentationSources()
            .filter(source => source.folder === 'azure_sdk_for_net_docs');
        expect(sources.every(source => source.path === 'docs/azure-sdk-for-net')).toBe(true);
        expect(sources.map(source => source.metadata)).toEqual(
            expectedPaths.map(() => ({ scope: 'branded' })));
        const repo = ConfigurationLoader.getRepositoryConfigs()
            .find(repository => repository.name === 'azure-sdk-for-net');
        expect(repo?.sparseCheckout).toEqual(expectedPaths);
    });

    it('applies identical metadata to .NET documents across all shared-root entries', () => {
        ConfigurationLoader.setConfigPath(configPath);
        const sources = ConfigurationLoader.getDocumentationSources()
            .filter(source => source.folder === 'azure_sdk_for_net_docs');
        expect(sources).toHaveLength(12);
        const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dotnet-metadata-'));
        tempDirs.push(root);
        for (const relativePath of [
            'eng/packages/http-client-csharp-mgmt/docs/emitter.md',
            'eng/packages/http-client-csharp/.tspd/docs/emitter.md',
            '.github/skills/mitigate-breaking-changes/SKILL.md',
            'sdk/core/Azure.Core.TestFramework/README.md',
        ]) {
            const file = path.join(root, relativePath);
            fs.mkdirSync(path.dirname(file), { recursive: true });
            fs.writeFileSync(file, '# .NET documentation');
            const results = sources.map(source => processMarkdownFile(file, source, root));
            expect(results.map(result => result.metadata)).toEqual(
                sources.map(() => ({ scope: 'branded' })));
            expect(new Set(results.map(result => result.blobPath)).size).toBe(1);
        }
    });

    it('preserves distinct repository-relative filenames for .NET emitter READMEs', () => {
        ConfigurationLoader.setConfigPath(configPath);
        const source = ConfigurationLoader.getDocumentationSources()
            .find(entry => entry.folder === 'azure_sdk_for_net_docs');
        if (!source) {
            throw new Error('Missing .NET source');
        }
        const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dotnet-citations-'));
        tempDirs.push(root);
        for (const emitter of ['http-client-csharp', 'http-client-csharp-mgmt']) {
            const relativePath = path.join('eng', 'packages', emitter, 'README.md');
            const file = path.join(root, relativePath);
            fs.mkdirSync(path.dirname(file), { recursive: true });
            fs.writeFileSync(file, '# Emitter documentation');
            const result = processMarkdownFile(file, source, root);
            expect(result.blobPath).toBe(
                `azure_sdk_for_net_docs/eng#packages#${emitter}#README.md`);
        }
    });

    it('registers upstream C# emitter docs with unbranded metadata and directory-relative filenames', () => {
        ConfigurationLoader.setConfigPath(configPath);
        const source = ConfigurationLoader.getDocumentationSources()
            .find(entry => entry.folder === 'typespec_csharp_emitter_docs');
        if (!source) {
            throw new Error('Missing upstream C# emitter source');
        }
        expect(path.normalize(source.path)).toBe(
            path.normalize('docs/typespec/packages/http-client-csharp/.tspd/docs'));
        expect(source.metadata).toEqual({ scope: 'unbranded' });

        const root = fs.mkdtempSync(path.join(os.tmpdir(), 'csharp-citations-'));
        tempDirs.push(root);
        for (const name of ['usage.md', 'customization.md']) {
            const file = path.join(root, name);
            fs.writeFileSync(file, '# C# emitter documentation');
            const result = processMarkdownFile(file, source, root);
            expect(result.blobPath).toBe(`typespec_csharp_emitter_docs/${name}`);
            expect(result.metadata).toEqual({ scope: 'unbranded' });
        }
    });
});
