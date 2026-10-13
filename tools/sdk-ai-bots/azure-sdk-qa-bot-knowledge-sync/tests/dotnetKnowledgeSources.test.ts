import { afterEach, describe, expect, it } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { ConfigurationLoader } from '../src/services/ConfigurationLoader';
import { processSourceDirectory } from '../src/DailySyncKnowledge';

const configPath = path.join(__dirname, '../config/knowledge-config.json');
const originalPath = ConfigurationLoader.getConfigPath();
const tempDirs: string[] = [];
const blobService = {
    hasContentChanged: () => true,
    hasMetadataChanged: () => false,
};

afterEach(() => {
    ConfigurationLoader.setConfigPath(originalPath);
    for (const dir of tempDirs.splice(0)) {
        fs.rmSync(dir, { recursive: true, force: true });
    }
});

function writeMarkdown(root: string, relativePath: string): void {
    const file = path.join(root, relativePath);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, '# Documentation\nContent');
}

describe('.NET knowledge configuration', () => {
    it('preserves unselected directory and individual-file processing behavior', async () => {
        const root = fs.mkdtempSync(path.join(os.tmpdir(), 'legacy-knowledge-'));
        tempDirs.push(root);
        const docs = path.join(root, 'docs');
        const target = path.join(root, 'target');
        fs.mkdirSync(target);
        writeMarkdown(docs, 'nested/guide.md');
        writeMarkdown(docs, 'ignored/guide.md');
        writeMarkdown(docs, 'reference/guide.md');
        writeMarkdown(docs, 'release-2026-01-01.md');
        const result = await processSourceDirectory(
            docs,
            { path: docs, folder: 'legacy', ignoredPaths: ['ignored'] },
            target,
            new Map(),
            blobService,
        );
        expect(result.changedFiles.map(file => file.blobPath)).toEqual(['legacy/nested#guide.md']);

        const file = path.join(docs, 'nested', 'guide.md');
        const fileResult = await processSourceDirectory(
            file, { path: file, folder: 'legacy' }, target, new Map(), blobService);
        expect(fileResult.changedFiles[0].blobPath).toBe('legacy/guide.md');
    });

    it('ingests every selected path once with repository-relative names and plane metadata', async () => {
        ConfigurationLoader.setConfigPath(configPath);
        const expectedPaths = [
            'doc',
            'sdk/resourcemanager/Azure.ResourceManager/docs',
            'eng/packages/http-client-csharp-mgmt/docs',
            'eng/packages/http-client-csharp-mgmt/README.md',
            'eng/packages/http-client-csharp/.tspd/docs',
            'eng/packages/http-client-csharp/README.md',
            '.github/skills/mitigate-breaking-changes',
            '.github/skills/mgmt-review-comment-resolution',
            'sdk/core/Azure.Core.TestFramework/README.md',
            'eng/common/testproxy/onboarding',
            'eng/common/tsp-client/README.md',
            'eng/common/knowledge/customizing-client-tsp.md',
        ];
        const sources = ConfigurationLoader.getDocumentationSources()
            .filter(source => source.folder === 'azure_sdk_for_net_docs');
        expect(sources.map(source => source.includePath)).toEqual(expectedPaths);
        expect(sources.every(source => source.path === 'docs/azure-sdk-for-net')).toBe(true);
        const repo = ConfigurationLoader.getRepositoryConfigs()
            .find(repository => repository.name === 'azure-sdk-for-net');
        expect(repo?.sparseCheckout).toEqual(expectedPaths.map(selected => `/${selected}`));

        const root = fs.mkdtempSync(path.join(os.tmpdir(), 'dotnet-knowledge-'));
        tempDirs.push(root);
        const repoRoot = path.join(root, 'repo');
        const target = path.join(root, 'target');
        fs.mkdirSync(target);
        const expectedFiles = expectedPaths.map(selected =>
            selected.endsWith('.md') ? selected : `${selected}/guide.md`);
        expectedFiles.push(
            '.github/skills/mitigate-breaking-changes/references/compatibility.md',
            '.github/skills/mgmt-review-comment-resolution/references/review.md',
            'eng/packages/http-client-csharp-mgmt/docs/index.mdx',
        );
        for (const file of expectedFiles) {
            writeMarkdown(repoRoot, file);
        }
        writeMarkdown(repoRoot, 'README.md');
        writeMarkdown(repoRoot, 'eng/unrelated/guide.md');
        writeMarkdown(repoRoot, '.github/skills/dpg-migration/SKILL.md');

        const blobs: string[] = [];
        for (const source of sources) {
            const result = await processSourceDirectory(repoRoot, source, target, new Map(), blobService);
            for (const file of result.changedFiles) {
                blobs.push(file.blobPath);
                expect(file.metadata).toEqual(source.metadata);
                expect(file.metadata?.scope).toBe('branded');
            }
        }
        expect(blobs.sort()).toEqual(expectedFiles.map(file =>
            `azure_sdk_for_net_docs/${file.replace(/\//g, '#')}`).sort());
        expect(new Set(blobs).size).toBe(blobs.length);
        expect(sources[2].metadata?.service_type).toBe('management-plane');
        expect(sources[4].metadata?.service_type).toBe('data-plane');
        expect(sources[6].metadata?.service_type).toBe('management-plane');
    });

    it('ingests upstream C# emitter docs as unbranded, relative to their own docs directory', async () => {
        ConfigurationLoader.setConfigPath(configPath);
        const source = ConfigurationLoader.getDocumentationSources()
            .find(entry => entry.folder === 'typespec_csharp_emitter_docs');
        expect(source).toBeDefined();
        if (!source) {
            throw new Error('Missing upstream C# emitter source');
        }
        expect(path.normalize(source.path)).toBe(
            path.normalize('docs/typespec/packages/http-client-csharp/.tspd/docs'));
        expect(source.includePath).toBeUndefined();
        expect(source.metadata).toEqual({ scope: 'unbranded' });

        const root = fs.mkdtempSync(path.join(os.tmpdir(), 'csharp-knowledge-'));
        tempDirs.push(root);
        const docs = path.join(root, 'docs');
        const target = path.join(root, 'target');
        fs.mkdirSync(target);
        writeMarkdown(docs, 'usage.md');
        writeMarkdown(docs, 'customization.md');
        const result = await processSourceDirectory(docs, source, target, new Map(), blobService);
        expect(result.changedFiles.map(file => file.blobPath).sort()).toEqual([
            'typespec_csharp_emitter_docs/customization.md',
            'typespec_csharp_emitter_docs/usage.md',
        ]);
        expect(result.changedFiles.every(file =>
            file.metadata?.scope === 'unbranded' && !file.metadata.service_type)).toBe(true);
    });
});
