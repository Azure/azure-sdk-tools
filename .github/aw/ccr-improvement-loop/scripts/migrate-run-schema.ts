#!/usr/bin/env node
/**
 * migrate-run-schema.ts — one-shot migration of persisted run JSON to the current
 * SCHEMA_VERSION.
 *
 * SCHEMA_VERSION 1.0 → 1.1 changed `run.id` from `${windowEnd}_${owner}_${repo}`
 * to the canonical window identity (window-id.ts). Historical files predate the
 * `cohort` concept and were all uncapped full-cohort runs, so they migrate to the
 * `uncapped` cohort. This recomputes `run.id`, bumps `schemaVersion`, renames each
 * file to `run-<newId>.json`, removes the stale name, revalidates against the
 * schema, and regenerates `manifest.json` for each `--dir`.
 *
 * Usage:
 *   node scripts/migrate-run-schema.ts --dir <runs-dir> [--dir <dir> ...] \
 *     [--file <single-run.json> ...]
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { parseRun, RAW_SCHEMA_VERSION, SCHEMA_VERSION } from "./run-schema.ts";
import { makeLogger } from "./utils.ts";
import { computeWindowId, runArtifactBase, type Cohort } from "./window-id.ts";

const log = makeLogger("migrate-run-schema");

interface LegacyRun {
    schemaVersion?: string;
    run?: {
        id?: string;
        repo?: string;
        windowStart?: string;
        windowEnd?: string;
        cohort?: Cohort;
    };
    [k: string]: unknown;
}

/** Recompute id + bump version on a parsed run object. Returns the new id. */
export function migrateRunObject(raw: LegacyRun): {
    id: string;
    version: string;
} {
    const run = raw.run;
    if (
        !run ||
        typeof run.repo !== "string" ||
        typeof run.windowStart !== "string" ||
        typeof run.windowEnd !== "string"
    ) {
        throw new Error("run is missing repo/windowStart/windowEnd");
    }
    const id = computeWindowId({
        repo: run.repo,
        windowStart: run.windowStart,
        windowEnd: run.windowEnd,
        cohort: run.cohort ?? "uncapped",
        rawSchemaVersion: RAW_SCHEMA_VERSION,
    });
    run.id = id;
    raw.schemaVersion = SCHEMA_VERSION;
    return { id, version: SCHEMA_VERSION };
}

/** Migrate one file in place (optionally renaming to canonical run-<id>.json). */
function migrateFile(file: string, rename: boolean): void {
    const raw = JSON.parse(fs.readFileSync(file, "utf8")) as LegacyRun;
    if (raw.schemaVersion === SCHEMA_VERSION) {
        log.error(`skip (already ${SCHEMA_VERSION}): ${file}`);
        return;
    }
    const { id } = migrateRunObject(raw);
    // Revalidate the migrated shape against the current schema.
    parseRun(raw);
    const text = JSON.stringify(raw, null, 2) + "\n";
    if (rename) {
        const dir = path.dirname(file);
        const target = path.join(dir, `${runArtifactBase(id)}.json`);
        fs.writeFileSync(target, text);
        if (path.resolve(target) !== path.resolve(file)) fs.rmSync(file);
        log.error(`migrated ${path.basename(file)} → ${path.basename(target)}`);
    } else {
        fs.writeFileSync(file, text);
        log.error(`migrated ${file} (in place)`);
    }
}

/** Regenerate `manifest.json` from the run-*.json present in a dir. */
function regenManifest(dir: string): void {
    const runs = fs
        .readdirSync(dir)
        .filter((f) => /^run-.*\.json$/.test(f))
        .sort();
    fs.writeFileSync(
        path.join(dir, "manifest.json"),
        JSON.stringify({ runs }, null, 2) + "\n",
    );
    log.error(`manifest ${dir}: ${String(runs.length)} run(s)`);
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            dir: { type: "string", multiple: true },
            file: { type: "string", multiple: true },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
        strict: true,
    });
    if (parsed.values.help) {
        process.stdout.write(
            "Usage: node scripts/migrate-run-schema.ts --dir <dir> [--file <run.json>]\n",
        );
        return;
    }
    const dirs = parsed.values.dir ?? [];
    const files = parsed.values.file ?? [];
    for (const dir of dirs) {
        for (const f of fs.readdirSync(dir)) {
            if (/^run-.*\.json$/.test(f)) migrateFile(path.join(dir, f), true);
        }
        regenManifest(dir);
    }
    for (const f of files) migrateFile(f, false);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    try {
        main();
    } catch (err: unknown) {
        log.error(err instanceof Error ? err.message : String(err));
        process.exit(1);
    }
}
