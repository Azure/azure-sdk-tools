import * as fs from 'fs';
import * as path from 'path';
import * as yaml from 'js-yaml';

const CONFIG_FILE = 'sample-config.yaml';
const GENERATED_DIR = 'generated';

/** Metadata stored in each sample's `sample-config.yaml`. */
interface SampleConfig {
    title?: string;
    description?: string;
    danger?: string;
}

/**
 * Converts TypeSpec sample folders (e.g. `packages/samples/specs`) into generated markdown.
 *
 * A sample is any folder containing a `sample-config.yaml` alongside one or more `.tsp` files.
 * Each `.tsp` file becomes a `## <fileName>` section with a fenced `typespec` block. Results are
 * written to a `generated/` subfolder so the regular pipeline can index them via an
 * `isGenerated` config entry.
 */
export class SampleProcessor {
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

        const sampleDirs = this.findSampleDirs(this.srcDir);
        console.log(`Found ${sampleDirs.length} sample folder(s) in ${this.srcDir}`);
        if (sampleDirs.length === 0) {
            return;
        }

        fs.mkdirSync(this.destDir, { recursive: true });
        for (const dir of sampleDirs) {
            try {
                this.renderSample(dir);
            } catch (error) {
                console.error(`Error converting sample ${dir}:`, error);
            }
        }
    }

    /** Recursively collect folders that contain a `sample-config.yaml`, skipping the output dir. */
    private findSampleDirs(root: string): string[] {
        const result: string[] = [];
        const walk = (dir: string): void => {
            if (path.resolve(dir) === path.resolve(this.destDir)) {
                return;
            }
            const entries = fs.readdirSync(dir, { withFileTypes: true });
            if (entries.some(e => e.isFile() && e.name === CONFIG_FILE)) {
                result.push(dir);
            }
            for (const entry of entries) {
                if (entry.isDirectory()) {
                    walk(path.join(dir, entry.name));
                }
            }
        };
        walk(root);
        return result.sort();
    }

    /** Render a single sample folder into one markdown document. */
    private renderSample(sampleDir: string): void {
        const tspFiles = fs
            .readdirSync(sampleDir, { withFileTypes: true })
            .filter(e => e.isFile() && e.name.endsWith('.tsp'))
            .map(e => e.name)
            .sort();

        if (tspFiles.length === 0) {
            console.warn(`No .tsp files in sample folder, skipping: ${sampleDir}`);
            return;
        }

        const config = this.readConfig(sampleDir);
        const relativeName = path.relative(this.srcDir, sampleDir) || path.basename(sampleDir);

        const lines: string[] = [`# ${config.title || relativeName}`, ''];
        if (config.description) {
            lines.push(config.description, '');
        }
        if (config.danger) {
            lines.push(config.danger, '');
        }
        for (const fileName of tspFiles) {
            const code = fs.readFileSync(path.join(sampleDir, fileName), 'utf-8').trimEnd();
            lines.push(`## ${fileName}`, '```typespec', code, '```', '');
        }

        const safeName = relativeName.replace(/[\\/]/g, '#');
        const outPath = path.join(this.destDir, `${safeName}.md`);
        fs.writeFileSync(outPath, lines.join('\n').trim() + '\n', 'utf-8');
        console.log(`Saved sample markdown to: ${outPath}`);
    }

    /** Read and parse a sample's `sample-config.yaml`; returns an empty config on failure. */
    private readConfig(sampleDir: string): SampleConfig {
        try {
            const raw = fs.readFileSync(path.join(sampleDir, CONFIG_FILE), 'utf-8');
            return (yaml.load(raw) as SampleConfig) ?? {};
        } catch (error) {
            console.warn(`Failed to read ${CONFIG_FILE} in ${sampleDir}:`, error);
            return {};
        }
    }

    private isDirectory(target: string): boolean {
        return fs.existsSync(target) && fs.statSync(target).isDirectory();
    }
}
