#!/usr/bin/env node
/**
 * classify-pr.ts — deterministic PR-type classification for metric normalization.
 * Precedence: labels > Conventional-Commit title prefix > linked-issue labels.
 * When no deterministic signal hits, leaves `prType: null` and
 * `classificationStatus: 'needs-agent'`. The agent then fills those rows in
 * `classified.json` per `references/classify-pr.prompt.md` (`agent` is a
 * *source*, never a value of PrType). No model is called from this script —
 * classification judgment is the agent's job in the workflow.
 */
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import { parseArgs as nodeParseArgs } from "node:util";

import type {
    ClassificationStatus,
    PrType,
    PrTypeSource,
    PullRequestData,
    PullRequestMetadata,
} from "./types.ts";
import { makeLogger } from "./utils.ts";

const log = makeLogger("classify-pr");

export interface PrClassification {
    prType: PrType | null;
    prTypeSource: PrTypeSource;
    classificationStatus: ClassificationStatus;
}

const LABEL_MAP: { match: RegExp; type: PrType }[] = [
    { match: /^(bug|bug-fix|bugfix|regression|defect)$/i, type: "bug-fix" },
    { match: /^(feature|enhancement|feat)$/i, type: "feature" },
    {
        match: /^(refactor|refactoring|cleanup|perf|performance)$/i,
        type: "refactor",
    },
    { match: /^(documentation|docs?)$/i, type: "docs" },
    { match: /^(test|tests|testing)$/i, type: "test" },
    {
        match: /^(chore|dependencies|deps|ci|build|infra|style)$/i,
        type: "chore",
    },
];

const TITLE_PREFIX_MAP: { prefix: string; type: PrType }[] = [
    { prefix: "fix", type: "bug-fix" },
    { prefix: "feat", type: "feature" },
    { prefix: "docs", type: "docs" },
    { prefix: "refactor", type: "refactor" },
    { prefix: "perf", type: "refactor" },
    { prefix: "test", type: "test" },
    { prefix: "chore", type: "chore" },
    { prefix: "build", type: "chore" },
    { prefix: "ci", type: "chore" },
    { prefix: "style", type: "chore" },
];

function fromLabels(labels: string[]): PrType | null {
    for (const label of labels) {
        for (const { match, type } of LABEL_MAP) {
            if (match.test(label.trim())) return type;
        }
    }
    return null;
}

function fromTitle(title: string): PrType | null {
    // Conventional-Commit prefix: `type` or `type(scope)` followed by `:`.
    const m = /^([a-z]+)(?:\([^)]*\))?!?:/i.exec(title.trim());
    if (!m) return null;
    const prefix = m[1]?.toLowerCase();
    for (const entry of TITLE_PREFIX_MAP) {
        if (entry.prefix === prefix) return entry.type;
    }
    return null;
}

/**
 * Resolve the deterministic classification for a PR. Returns `needs-agent` when
 * no label/title/issue signal applies — those rows are filled by the agent in
 * `classified.json`, not by this script.
 */
export function classifyPr(meta: PullRequestMetadata): PrClassification {
    const labels = meta.labels ?? [];
    const labelType = fromLabels(labels);
    if (labelType) {
        return {
            prType: labelType,
            prTypeSource: "label",
            classificationStatus: "complete",
        };
    }

    const titleType = fromTitle(meta.title);
    if (titleType) {
        return {
            prType: titleType,
            prTypeSource: "title",
            classificationStatus: "complete",
        };
    }

    const issueLabels = (meta.linkedIssues ?? []).flatMap((i) => i.labels);
    const issueType = fromLabels(issueLabels);
    if (issueType) {
        return {
            prType: issueType,
            prTypeSource: "issue",
            classificationStatus: "complete",
        };
    }

    return {
        prType: null,
        prTypeSource: "unknown",
        classificationStatus: "needs-agent",
    };
}

function usage(): string {
    return [
        "Usage:",
        "  node scripts/classify-pr.ts --glob 'pr-cache/<owner>-<repo>/pr-*.json' [options]",
        "",
        "Options:",
        "  --glob <pattern>       Raw PR cache files to classify",
        "  <pr-file.json> ...     Explicit files",
        "  --cache-dir <path>     Write classified.json here (else stdout)",
        "  --json                 Emit machine-readable result to stdout",
        "  -h, --help",
        "",
        "needs-agent PRs are left with prType:null; the agent fills them in",
        "classified.json per references/classify-pr.prompt.md.",
    ].join("\n");
}

function main(): void {
    const parsed = nodeParseArgs({
        args: process.argv.slice(2),
        options: {
            glob: { type: "string" },
            "cache-dir": { type: "string" },
            json: { type: "boolean", default: false },
            help: { type: "boolean", short: "h", default: false },
        },
        allowPositionals: true,
        strict: true,
    });
    if (parsed.values.help) {
        process.stdout.write(`${usage()}\n`);
        return;
    }

    const files = [...parsed.positionals];
    if (parsed.values.glob) {
        files.push(
            ...fs.globSync(parsed.values.glob, { withFileTypes: false }),
        );
    }
    if (files.length === 0)
        throw new Error("no input files (pass paths or --glob)");

    const rows: (PullRequestMetadata & PrClassification)[] = [];
    let needsAgent = 0;
    for (const file of files) {
        const data = JSON.parse(
            fs.readFileSync(file, "utf8"),
        ) as PullRequestData;
        const cls = classifyPr(data.pr);
        if (cls.classificationStatus === "needs-agent") needsAgent++;
        rows.push({ ...data.pr, ...cls });
    }

    const payload = {
        processed: rows.length,
        kept: rows.length,
        skipped: 0,
        skipReasons: { needsAgent },
        prs: rows,
    };

    if (parsed.values["cache-dir"]) {
        fs.mkdirSync(parsed.values["cache-dir"], { recursive: true });
        const out = `${parsed.values["cache-dir"]}/classified.json`;
        fs.writeFileSync(out, JSON.stringify(payload, null, 2));
        log.error(
            `wrote ${out} (${rows.length} PRs, ${needsAgent} need agent)`,
        );
    }
    if (parsed.values.json || !parsed.values["cache-dir"]) {
        process.stdout.write(JSON.stringify(payload, null, 2) + "\n");
    }
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
    try {
        main();
    } catch (err: unknown) {
        log.error(err instanceof Error ? err.message : String(err));
        process.exit(1);
    }
}
