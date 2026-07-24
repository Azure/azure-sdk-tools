#!/usr/bin/env node
/**
 * gen-manifest.ts — regenerate dashboard/data/manifest.json from the run files on
 * disk.
 *
 * The static dashboard cannot list a directory over `file://`/HTTP, so it reads a
 * checked-in `manifest.json` naming every `run-*.json` to load. Hand-maintaining
 * that list rots the moment a backfill adds a month; this scans the data dir and
 * rewrites the manifest deterministically (sorted), so "add runs" and "publish"
 * stay a single mechanical step with no dashboard edit.
 *
 * Pure list-building lives in {@link buildManifest} (unit-tested); the CLI is just
 * a readdir + write around it.
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import { makeLogger } from "./utils.ts";

const log = makeLogger("gen-manifest");

/** A run file is `run-*.json` (never the manifest itself or any other artifact). */
export function isRunFile(name: string): boolean {
    return name.startsWith("run-") && name.endsWith(".json");
}

export interface Manifest {
    runs: string[];
}

/**
 * Build the manifest from a directory listing: keep only run files, sort them
 * deterministically (byte order, stable across platforms), and drop duplicates.
 * Pure — no I/O — so it is exhaustively unit-testable.
 */
export function buildManifest(entries: string[]): Manifest {
    const runs = [...new Set(entries.filter(isRunFile))].sort((a, b) =>
        a < b ? -1 : a > b ? 1 : 0,
    );
    return { runs };
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/gen-manifest.ts --data-dir <path> [--check]",
        "",
        "Options:",
        "  --data-dir <path>   Directory holding run-*.json (required)",
        "  --check             Exit non-zero if manifest.json is stale (no write)",
        "  -h, --help",
    ].join("\n");
}

function main(): void {
    const { values } = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            "data-dir": { type: "string" },
            check: { type: "boolean", default: false },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: false,
        strict: true,
    });
    if (values.help) {
        process.stdout.write(`${usage()}\n`);
        return;
    }
    if (!values["data-dir"]) throw new Error("--data-dir is required");
    const dataDir = values["data-dir"];
    const entries = fs.readdirSync(dataDir);
    const manifest = buildManifest(entries);
    const out = `${JSON.stringify(manifest, null, 2)}\n`;
    const manifestPath = path.join(dataDir, "manifest.json");

    if (values.check) {
        const current = fs.existsSync(manifestPath)
            ? fs.readFileSync(manifestPath, "utf8")
            : "";
        if (current !== out) {
            log.error(
                `manifest.json is stale (${String(
                    manifest.runs.length,
                )} run files on disk). Run: node scripts/gen-manifest.ts --data-dir ${dataDir}`,
            );
            process.exitCode = 1;
            return;
        }
        log.error(
            `manifest.json is up to date (${String(manifest.runs.length)} runs)`,
        );
        return;
    }

    fs.writeFileSync(manifestPath, out);
    log.error(`wrote ${manifestPath} (${String(manifest.runs.length)} runs)`);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    main();
}
