import { afterEach, describe, expect, it, vi } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import {
    ConfigurationLoader,
    DocumentationPath,
    DocumentationSource,
    KnowledgeConfig,
} from '../src/services/ConfigurationLoader';
import { createReleaseNotesIndex, processSourceDirectory } from '../src/DailySyncKnowledge';

const originalConfigPath = ConfigurationLoader.getConfigPath();
const tempDirs: string[] = [];
const blobService = {
    hasContentChanged: vi.fn(() => true),
    hasMetadataChanged: vi.fn(() => false),
};

afterEach(() => {
    ConfigurationLoader.setConfigPath(originalConfigPath);
    vi.clearAllMocks();
    for (const dir of tempDirs.splice(0)) {
        fs.rmSync(dir, { recursive: true, force: true });
    }
});

function createFixture(): { root: string; repoRoot: string; target: string } {
    const root = fs.mkdtempSync(path.join(os.tmpdir(), 'knowledge-path-scoping-'));
    tempDirs.push(root);
    const repoRoot = path.join(root, 'repo');
    const target = path.join(root, 'target');
    fs.mkdirSync(repoRoot);
    fs.mkdirSync(target);
    return { root, repoRoot, target };
}

function writeMarkdown(root: string, relativePath: string, content = '# Documentation\nContent'): void {
    const file = path.join(root, relativePath);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, content);
}

function loadSources(root: string, paths: DocumentationPath[]): DocumentationSource[] {
    const config: KnowledgeConfig = {
        version: '1.0.0',
        sources: [{
            repository: {
                url: 'https://github.com/example/fixture-repo.git',
                path: 'fixture-checkout',
                branch: 'main',
                authType: 'public',
            },
            paths,
        }],
    };
    const configPath = path.join(root, 'knowledge-config.json');
    fs.writeFileSync(configPath, JSON.stringify(config));
    ConfigurationLoader.setConfigPath(configPath);
    return ConfigurationLoader.getDocumentationSources();
}

describe('source path scoping', () => {
    it('separates repository citation roots from directory/file selections and sparse checkout', () => {
        const { root } = createFixture();
        const sources = loadSources(root, [
            { name: 'Guide', description: '', path: '/guides', folder: 'shared', relativeByRepoPath: true },
            { name: 'Readme', description: '', path: '\\tools\\widget\\README.md', folder: 'shared', relativeByRepoPath: true },
            { name: 'Ordinary docs', description: '', path: 'ordinary/docs', folder: 'ordinary' },
            { name: 'Ordinary file', description: '', path: 'ordinary/README.md', folder: 'ordinary' },
        ]);

        expect(sources.map(source => source.path)).toEqual([
            'docs/fixture-checkout',
            'docs/fixture-checkout',
            'docs/fixture-checkout/ordinary/docs',
            'docs/fixture-checkout/ordinary/README.md',
        ]);
        expect(sources.map(source => source.includePath)).toEqual([
            'guides', 'tools\\widget\\README.md', undefined, undefined,
        ]);
        expect(ConfigurationLoader.getRepositoryConfigs()[0].sparseCheckout).toEqual([
            '/guides', '\\tools\\widget\\README.md', 'ordinary/docs', 'ordinary/README.md',
        ]);
    });

    it('ingests disjoint shared-folder entries once with stable filenames and independent metadata', async () => {
        const { root, repoRoot, target } = createFixture();
        const sources = loadSources(root, [
            {
                name: 'Management', description: '', path: '/guides/management', folder: 'shared',
                relativeByRepoPath: true, metadata: { scope: 'branded', service_type: 'management-plane' },
            },
            {
                name: 'Data', description: '', path: '/guides/data', folder: 'shared',
                relativeByRepoPath: true, metadata: { scope: 'branded', service_type: 'data-plane' },
            },
            {
                name: 'Skill', description: '', path: '/skills/review', folder: 'shared',
                relativeByRepoPath: true, metadata: { scope: 'unbranded' },
            },
        ]);
        const selectedFiles = [
            'guides/management/guide.md',
            'guides/management/index.mdx',
            'guides/data/guide.md',
            'skills/review/SKILL.md',
            'skills/review/references/details.md',
        ];
        for (const file of [...selectedFiles, 'README.md', 'guides/README.md', 'unrelated/guide.md', 'skills/other/SKILL.md']) {
            writeMarkdown(repoRoot, file);
        }

        const blobs: string[] = [];
        for (const source of sources) {
            const result = await processSourceDirectory(repoRoot, source, target, new Map(), blobService);
            expect(result.totalProcessed).toBe(result.changedFiles.length);
            for (const file of result.changedFiles) {
                blobs.push(file.blobPath);
                expect(file.blobPath).toBe(`shared/${file.filename}`);
                expect(file.metadata).toEqual(source.metadata);
                expect(fs.readFileSync(path.join(target, file.filename), 'utf-8')).toBe(file.content);
            }
        }
        expect(blobs.sort()).toEqual(selectedFiles.map(file => `shared/${file.replace(/\//g, '#')}`).sort());
        expect(new Set(blobs).size).toBe(blobs.length);
        expect(blobService.hasContentChanged).toHaveBeenCalledTimes(selectedFiles.length);
        expect(blobService.hasMetadataChanged).toHaveBeenCalledTimes(selectedFiles.length);
    });

    it('preserves repository-relative identities for selected files with the same basename', async () => {
        const { root, repoRoot, target } = createFixture();
        const sources = loadSources(root, ['tools/one/README.md', 'tools/two/README.md'].map(selected => ({
            name: selected, description: '', path: `/${selected}`, folder: 'shared', relativeByRepoPath: true,
        })));
        writeMarkdown(repoRoot, 'tools/one/README.md', '# One\nFirst content');
        writeMarkdown(repoRoot, 'tools/two/README.md', '# Two\nSecond content');
        writeMarkdown(repoRoot, 'tools/one/unselected.md');
        const filenames: string[] = [];
        for (const source of sources) {
            const result = await processSourceDirectory(repoRoot, source, target, new Map(), blobService);
            expect(result.totalProcessed).toBe(1);
            filenames.push(result.changedFiles[0].filename);
        }
        expect(filenames).toEqual(['tools#one#README.md', 'tools#two#README.md']);
        expect(fs.readFileSync(path.join(target, filenames[0]), 'utf-8')).toContain('First content');
        expect(fs.readFileSync(path.join(target, filenames[1]), 'utf-8')).toContain('Second content');
    });

    it('keeps pathless repository-relative sources scanning the whole available repository', async () => {
        const { root, repoRoot, target } = createFixture();
        const [source] = loadSources(root, [{
            name: 'Entire repo', description: '', folder: 'whole_repo', relativeByRepoPath: true,
        }]);
        expect(source.path).toBe('docs/fixture-checkout');
        expect(source.includePath).toBeUndefined();
        expect(ConfigurationLoader.getRepositoryConfigs()[0].sparseCheckout).toBeUndefined();
        writeMarkdown(repoRoot, 'README.md');
        writeMarkdown(repoRoot, 'nested/guide.mdx');
        const result = await processSourceDirectory(repoRoot, source, target, new Map(), blobService);
        expect(result.changedFiles.map(file => file.blobPath).sort()).toEqual([
            'whole_repo/README.md', 'whole_repo/nested#guide.mdx',
        ]);
    });

    it('preserves ordinary directory/file identities and ignored/reference/dated-release filters', async () => {
        const { root, repoRoot, target } = createFixture();
        const [source, fileSource] = loadSources(root, [
            { name: 'Docs', description: '', path: 'docs', folder: 'ordinary', ignoredPaths: ['ignored'] },
            { name: 'File', description: '', path: 'docs/nested/guide.md', folder: 'ordinary' },
        ]);
        for (const file of [
            'nested/guide.md', 'nested/index.mdx', 'ignored/guide.md', 'reference/guide.md',
            'release-2026-01-01.md', 'nested/release-2026-01-02.mdx',
        ]) {
            writeMarkdown(repoRoot, `docs/${file}`);
        }
        fs.writeFileSync(path.join(repoRoot, 'docs', 'not-markdown.txt'), 'Unselected');
        const result = await processSourceDirectory(
            path.join(repoRoot, 'docs'), source, target, new Map(), blobService);
        expect(result.changedFiles.map(file => file.blobPath).sort()).toEqual([
            'ordinary/nested#guide.md', 'ordinary/nested#index.mdx',
        ]);
        const fileResult = await processSourceDirectory(
            path.join(repoRoot, 'docs', 'nested', 'guide.md'), fileSource, target, new Map(), blobService);
        expect(fileResult.changedFiles.map(file => file.blobPath)).toEqual(['ordinary/guide.md']);
    });

    it('keeps ignored paths and metadata overrides relative to the repository root', async () => {
        const { root, repoRoot, target } = createFixture();
        const [source] = loadSources(root, [{
            name: 'Selected docs', description: '', path: '/docs', folder: 'shared', relativeByRepoPath: true,
            ignoredPaths: [path.join('docs', 'ignored')],
            metadata: { scope: 'branded', service_type: 'data-plane' },
            overrides: [{
                pattern: 'docs/management/**',
                metadata: { scope: 'branded', service_type: 'management-plane' },
            }],
        }]);
        writeMarkdown(repoRoot, 'docs/data/guide.md');
        writeMarkdown(repoRoot, 'docs/management/guide.md');
        writeMarkdown(repoRoot, 'docs/ignored/guide.md');
        writeMarkdown(repoRoot, 'docs/release-2026-01-01.mdx');
        const result = await processSourceDirectory(repoRoot, source, target, new Map(), blobService);
        expect(result.changedFiles.map(file => [file.blobPath, file.metadata?.service_type]).sort()).toEqual([
            ['shared/docs#data#guide.md', 'data-plane'],
            ['shared/docs#management#guide.md', 'management-plane'],
        ]);
    });

    it('reports a missing selected path instead of falling back to scanning the repository', async () => {
        const { repoRoot, target } = createFixture();
        writeMarkdown(repoRoot, 'README.md');
        await expect(processSourceDirectory(
            repoRoot, { path: repoRoot, includePath: 'missing', folder: 'shared' },
            target, new Map(), blobService,
        )).rejects.toMatchObject({ code: 'ENOENT' });
        expect(blobService.hasContentChanged).not.toHaveBeenCalled();
        expect(fs.readdirSync(target)).toEqual([]);
    });

    it('skips root release-note discovery when it is outside a selected directory or file', async () => {
        const { repoRoot, target } = createFixture();
        writeMarkdown(repoRoot, 'release-notes/release-2026-01-01.md');
        writeMarkdown(repoRoot, 'docs/guide.md');
        for (const includePath of ['docs', 'docs/guide.md', 'release-notes/release-2026-01-01.md']) {
            await createReleaseNotesIndex(
                { path: repoRoot, includePath, folder: 'typespec_docs' }, repoRoot, target);
            expect(fs.readdirSync(target)).toEqual([]);
        }
    });

    it('retains existing root release-note discovery when the selection includes that directory', async () => {
        const { repoRoot, target } = createFixture();
        writeMarkdown(repoRoot, 'release-notes/release-2026-01-01.md',
            '---\ntitle: January\nreleaseDate: 2026-01-01\n---\n## Features\nNew feature');
        for (const includePath of [undefined, 'release-notes', '.']) {
            await createReleaseNotesIndex(
                { path: repoRoot, includePath, folder: 'typespec_docs' }, repoRoot, target);
            const index = fs.readFileSync(path.join(target, 'version-release-notes-index.md'), 'utf-8');
            expect(index).toContain('January');
            expect(index).toContain('New feature');
            fs.unlinkSync(path.join(target, 'version-release-notes-index.md'));
        }
    });

    it('does not introduce discovery of nested release-note directories', async () => {
        const { repoRoot, target } = createFixture();
        writeMarkdown(repoRoot, 'docs/release-notes/release-2026-01-01.md');
        await createReleaseNotesIndex(
            { path: repoRoot, includePath: 'docs', folder: 'typespec_docs' }, repoRoot, target);
        expect(fs.readdirSync(target)).toEqual([]);
    });
});
