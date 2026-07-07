import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { parseRun, RunSchema, SCHEMA_VERSION } from "../scripts/run-schema.ts";

const here = dirname(fileURLToPath(import.meta.url));
const sample: unknown = JSON.parse(
    readFileSync(join(here, "fixtures", "run-sample.json"), "utf8"),
);

describe("run-schema", () => {
    it("accepts a valid sample run", () => {
        const parsed = parseRun(sample);
        expect(parsed.schemaVersion).toBe(SCHEMA_VERSION);
        expect(parsed.run.id).toBe("2026-06-18_Azure_azure-sdk-for-go");
    });

    it("rejects an unknown top-level field", () => {
        const bad = { ...(sample as object), extraField: true };
        expect(() => parseRun(bad)).toThrow();
    });

    it("rejects an unknown field inside a comment row", () => {
        const clone = structuredClone(sample) as { comments: object[] };
        clone.comments[0] = { ...clone.comments[0], legacyField: 1 };
        expect(() => parseRun(clone)).toThrow();
    });

    it("rejects a legacy snake_case field name", () => {
        const clone = structuredClone(sample) as {
            comments: Record<string, unknown>[];
        };
        const c = clone.comments[0]!;
        const renamed: Record<string, unknown> = {
            ...c,
            line_start: c.lineStart,
        };
        delete renamed.lineStart;
        clone.comments[0] = renamed;
        expect(() => parseRun(clone)).toThrow();
    });

    it("round-trips content-stably excluding generatedAt", () => {
        const parsed = parseRun(sample);
        const reSerialized: unknown = JSON.parse(JSON.stringify(parsed));
        const reParsed = parseRun(reSerialized);

        const strip = (r: ReturnType<typeof parseRun>): unknown => {
            const copy = structuredClone(r);
            copy.run.generatedAt = "<volatile>";
            return copy;
        };
        expect(strip(reParsed)).toEqual(strip(parsed));
    });

    it("requires the current schemaVersion literal", () => {
        const clone = structuredClone(sample) as { schemaVersion: string };
        clone.schemaVersion = "0.9";
        expect(() => RunSchema.parse(clone)).toThrow();
    });

    it("allows experiment to be null", () => {
        const parsed = parseRun(sample);
        expect(parsed.experiment).toBeNull();
    });
});
