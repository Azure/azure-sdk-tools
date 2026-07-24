import { describe, expect, it } from "vitest";

import {
    cohortOf,
    computeWindowId,
    ownerRepoOf,
    runArtifactBase,
} from "../scripts/window-id.ts";
import { RAW_SCHEMA_VERSION } from "../scripts/run-schema.ts";

const BASE = {
    repo: "Azure/azure-sdk-for-go",
    windowStart: "2026-06-01",
    windowEnd: "2026-06-18",
    cohort: "uncapped" as const,
};

describe("computeWindowId — canonical identity", () => {
    it("encodes owner_repo__start__end__cohort__raw<version>", () => {
        expect(computeWindowId(BASE)).toBe(
            `Azure_azure-sdk-for-go__2026-06-01__2026-06-18__uncapped__raw${RAW_SCHEMA_VERSION}`,
        );
    });

    it("is stable for identical inputs", () => {
        expect(computeWindowId(BASE)).toBe(computeWindowId({ ...BASE }));
    });

    it("changes when the repo changes", () => {
        expect(
            computeWindowId({ ...BASE, repo: "Azure/azure-sdk-for-py" }),
        ).not.toBe(computeWindowId(BASE));
    });

    it("changes when the window start changes", () => {
        expect(
            computeWindowId({ ...BASE, windowStart: "2026-06-02" }),
        ).not.toBe(computeWindowId(BASE));
    });

    it("changes when the window end changes", () => {
        expect(computeWindowId({ ...BASE, windowEnd: "2026-06-19" })).not.toBe(
            computeWindowId(BASE),
        );
    });

    it("changes when the cohort changes", () => {
        expect(computeWindowId({ ...BASE, cohort: "cap-100" })).not.toBe(
            computeWindowId(BASE),
        );
    });

    it("changes when the raw schema version changes", () => {
        expect(computeWindowId({ ...BASE, rawSchemaVersion: "9.9" })).not.toBe(
            computeWindowId(BASE),
        );
    });

    it("rejects a malformed repo", () => {
        expect(() => ownerRepoOf("nope")).toThrow();
        expect(() => computeWindowId({ ...BASE, repo: "nope" })).toThrow();
    });
});

describe("cohortOf", () => {
    it("maps 0 / negative / non-finite to uncapped", () => {
        expect(cohortOf(0)).toBe("uncapped");
        expect(cohortOf(-5)).toBe("uncapped");
        expect(cohortOf(Number.NaN)).toBe("uncapped");
    });
    it("maps a positive cap to cap-<N>", () => {
        expect(cohortOf(100)).toBe("cap-100");
        expect(cohortOf(25.9)).toBe("cap-25");
    });
});

describe("runArtifactBase / dashboard filename compatibility", () => {
    it("prefixes run- so basename copy + manifest globbing still match", () => {
        const id = computeWindowId(BASE);
        const filename = `${runArtifactBase(id)}.json`;
        // The ccr-dashboard-publish.yml copy-by-basename + manifest regen use
        // this exact pattern; a windowId filename (with __ and a dot in raw1.0)
        // must still match.
        expect(runArtifactBase(id)).toBe(`run-${id}`);
        expect(/^run-.*\.json$/.test(filename)).toBe(true);
    });
});
