import { describe, it } from "vitest";
import assert from "node:assert/strict";

import {
  selectCandidates,
  summarizePrData,
} from "../scripts/pick-pr-candidates.ts";
import { isHumanUser } from "../scripts/utils.ts";
import type { PullRequestData } from "../scripts/types.ts";

function makePrData(
  number: number,
  inline: number,
  review: number,
  issue: number,
): PullRequestData {
  return {
    pr: {
      number,
      title: `PR ${number}`,
      author: { login: "alice", type: "User" },
      url: `https://github.com/owner/repo/pull/${number}`,
      state: "closed",
      mergedAt: "2026-05-29T00:00:00Z",
    },
    reviews: Array.from({ length: review }, (_, i) => ({
      id: i + 1,
      state: "COMMENTED",
      body: "review",
      submitted_at: "2026-05-29T00:00:00Z",
      user: { login: "carol", type: "User" },
    })),
    inline: Array.from({ length: inline }, (_, i) => ({
      id: i + 1,
      path: "src/app.ts",
      line: i + 1,
      body: "inline",
      user: { login: "dave", type: "User" },
    })),
    issue: Array.from({ length: issue }, (_, i) => ({
      id: i + 1,
      body: "issue",
      created_at: "2026-05-29T00:00:00Z",
      user: { login: "eve", type: "User" },
    })),
  };
}

describe("pick-pr-candidates helpers", () => {
  it("identifies human users", () => {
    assert.equal(isHumanUser({ login: "alice", type: "User" }), true);
    assert.equal(
      isHumanUser({ login: "dependabot[bot]", type: "User" }),
      false,
    );
    assert.equal(isHumanUser({ login: "copilot", type: "Bot" }), false);
    assert.equal(isHumanUser(null), false);
  });

  it("summarizes per-source human counts", () => {
    const summary = summarizePrData(makePrData(42, 2, 1, 3));
    assert.equal(summary.pr, 42);
    assert.equal(summary.humanInline, 2);
    assert.equal(summary.humanReview, 1);
    assert.equal(summary.humanIssue, 3);
    assert.equal(summary.humanTotal, 6);
  });

  it("filters and ranks candidates for iterative waves", () => {
    const all = [
      summarizePrData(makePrData(100, 3, 0, 0)),
      summarizePrData(makePrData(101, 1, 2, 0)),
      summarizePrData(makePrData(102, 0, 5, 0)),
      summarizePrData(makePrData(103, 2, 1, 1)),
    ];

    const selected = selectCandidates(all, {
      glob: "unused",
      exclude: new Set([100]),
      minInline: 1,
      minTotal: 2,
      limit: 5,
      format: "summary",
    });

    // #103 beats #101 on inline count tie-break then total count.
    assert.deepEqual(
      selected.map((c) => c.pr),
      [103, 101],
    );
  });
});
