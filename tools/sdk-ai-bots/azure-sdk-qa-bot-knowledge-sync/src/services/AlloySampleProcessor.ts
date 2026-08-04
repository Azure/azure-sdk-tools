import * as fs from 'fs';
import * as path from 'path';

const GENERATED_DIR = 'generated';
const SAMPLES_BASE_URL = 'https://github.com/alloy-framework/alloy/tree/main/samples';
const IGNORED_DIR_NAMES = new Set(['node_modules', 'dist', '.temp', GENERATED_DIR]);
const IGNORED_FILE_SUFFIXES = ['.config.ts', '.config.js', '.config.mjs'];
const LANGUAGE_BY_EXTENSION: Record<string, string> = {
    '.ts': 'ts',
    '.tsx': 'tsx',
    '.js': 'js',
    '.jsx': 'jsx',
    '.go': 'go',
    '.py': 'python',
    '.cs': 'csharp',
    '.java': 'java'
};

/**
 * Converts Alloy sample project folders (e.g. `samples/basic-project`) into generated markdown.
 *
 * Each immediate subfolder of the samples directory is treated as one sample. Every source file
 * under it (excluding build/test tooling like `package.json` or `vitest.config.ts`) becomes a
 * `## <relativePath>` section with a fenced code block. Results are written to a `generated/`
 * subfolder so the regular pipeline can index them via an `isGenerated` config entry.
 */
export class AlloySampleProcessor {
    private readonly srcDir: string;
    private readonly destDir: string;

    constructor(workDir: string, relativeSamplesDir: string) {
        this.srcDir = path.join(workDir, relativeSamplesDir);
        this.destDir = path.join(this.srcDir, GENERATED_DIR);
    }

    /** Render every sample folder under the samples directory into a markdown file. */
    public processSamples(): void {
        if (!this.isDirectory(this.srcDir)) {
            console.error(`Samples directory not found: ${this.srcDir}`);
            return;
        }

        const sampleNames = fs
            .readdirSync(this.srcDir, { withFileTypes: true })
            .filter(e => e.isDirectory() && !IGNORED_DIR_NAMES.has(e.name))
            .map(e => e.name)
            .sort();

        console.log(`Found ${sampleNames.length} sample folder(s) in ${this.srcDir}`);
        if (sampleNames.length === 0) {
            return;
        }

        fs.mkdirSync(this.destDir, { recursive: true });
        for (const name of sampleNames) {
            try {
                this.renderSample(name);
            } catch (error) {
                console.error(`Error converting sample ${name}:`, error);
            }
        }
    }

    /** Render a single sample folder into one markdown document. */
    private renderSample(sampleName: string): void {
        const sampleDir = path.join(this.srcDir, sampleName);
        const sourceFiles = this.findSourceFiles(sampleDir);

        if (sourceFiles.length === 0) {
            console.warn(`No source files in sample folder, skipping: ${sampleDir}`);
            return;
        }

        // Name the framework in the title so these chunks are retrievable by "alloy"/"emitter" queries.
        const lines: string[] = [
            `# Alloy framework sample: ${sampleName}`,
            '',
            `Source files of the \`${sampleName}\` sample project from the Alloy code generation framework, ` +
                `which the TypeSpec emitter framework (EF v2) builds on (build/test/config files omitted). ` +
                `Source: ${SAMPLES_BASE_URL}/${sampleName}`, 
            ''
        ];
        for (const filePath of sourceFiles) {
            const relativeName = path.relative(sampleDir, filePath).replace(/\\/g, '/');
            const language = LANGUAGE_BY_EXTENSION[path.extname(filePath)] ?? '';
            const code = fs.readFileSync(filePath, 'utf-8').trimEnd();
            lines.push(`## ${relativeName}`, '```' + language, code, '```', '');
        }

        const outPath = path.join(this.destDir, `${sampleName}.md`);
        fs.writeFileSync(outPath, lines.join('\n').trim() + '\n', 'utf-8');
        console.log(`Saved sample markdown to: ${outPath}`);
    }

    /** Recursively collect known source files under a sample folder, skipping build/test tooling. */
    private findSourceFiles(root: string): string[] {
        const result: string[] = [];
        const walk = (dir: string): void => {
            const entries = fs.readdirSync(dir, { withFileTypes: true });
            for (const entry of entries) {
                if (entry.isDirectory()) {
                    if (!IGNORED_DIR_NAMES.has(entry.name)) {
                        walk(path.join(dir, entry.name));
                    }
                    continue;
                }
                if (IGNORED_FILE_SUFFIXES.some(suffix => entry.name.endsWith(suffix))) {
                    continue;
                }
                if (path.extname(entry.name) in LANGUAGE_BY_EXTENSION) {
                    result.push(path.join(dir, entry.name));
                }
            }
        };
        walk(root);
        return result.sort();
    }

    private isDirectory(dirPath: string): boolean {
        return fs.existsSync(dirPath) && fs.statSync(dirPath).isDirectory();
    }
}
