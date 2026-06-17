import { describe, it } from "vitest";
import assert from "node:assert/strict";
import { buildSearch } from "../scripts/fetch-prs.ts";

describe("buildSearch", () => {
    it("returns empty string when no filters are set", () => {
        assert.equal(
            buildSearch({
                labels: [],
                reviewers: [],
            } as never),
            "",
        );
    });

    it("combines structured filters in a stable order", () => {
        const search = buildSearch({
            since: "2026-01-01",
            author: "alice",
            labels: ["area-cli", "bug"],
            reviewers: ["carol", "dave"],
            search: "is:merged sort:updated-desc",
        } as never);

        assert.equal(
            search,
            'merged:>=2026-01-01 author:alice label:"area-cli" label:"bug" reviewer:carol reviewer:dave is:merged sort:updated-desc',
        );
    });
});
