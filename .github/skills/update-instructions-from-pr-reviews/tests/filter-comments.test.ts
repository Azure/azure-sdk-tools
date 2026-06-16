import { describe, it } from "vitest";
import assert from "node:assert/strict";
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";

import {
  AUTOMATION_MARKERS,
  AUTHOR_REPLY_PATTERNS,
  classify,
  DEFAULT_BOT_LOGINS,
  filterPullRequestData,
  inferKind,
  isAutomationBoilerplate,
  isBot,
  isQuotedOnly,
  LOW_SIGNAL,
  parseArgs,
  shouldSkipPr,
} from "../scripts/filter-comments.ts";
import type { FilterOpts, PullRequestData } from "../scripts/types.ts";

const __dirname = path.dirname(fileURLToPath(import.meta.url));

const FIXTURE = JSON.parse(
  fs.readFileSync(path.join(__dirname, "fixtures", "pr-sample.json"), "utf8"),
) as PullRequestData;

const DEFAULTS: FilterOpts = {
  minLength: 20,
  includeSelf: false,
};

describe("isBot", () => {
  it("detects User type Bot", () => {
    assert.equal(isBot({ login: "x", type: "Bot" }), true);
  });

  it("detects [bot] suffix login", () => {
    assert.equal(isBot({ login: "dependabot[bot]", type: "User" }), true);
  });

  it("returns false for humans and null", () => {
    assert.equal(isBot({ login: "alice", type: "User" }), false);
    assert.equal(isBot(null), false);
  });
});

describe("isQuotedOnly", () => {
  it("detects all-quoted body", () => {
    assert.equal(isQuotedOnly("> a\n> b"), true);
    assert.equal(isQuotedOnly("> a\n\n> b"), true);
  });

  it("returns false when there is new content", () => {
    assert.equal(isQuotedOnly("> a\nthanks for the heads up"), false);
  });
});

describe("parseArgs", () => {
  it("includes PR author self-comments by default", () => {
    const opts = parseArgs([
      path.join(__dirname, "fixtures", "pr-sample.json"),
    ]);
    assert.equal(opts.includeSelf, true);
  });
});

describe("classify", () => {
  describe("with default options", () => {
    it("drops bot author", () => {
      const c = {
        body: "this is a long enough comment to pass minLength",
        user: { login: "x", type: "Bot" as const },
      };
      assert.equal(classify(c, "alice", 20, false), "bot");
    });

    it("drops self comments when includeSelf is false", () => {
      const c = {
        body: "Will rebase and address feedback in the next push.",
        user: { login: "alice", type: "User" as const },
      };
      assert.equal(classify(c, "alice", 20, false), "self");
    });

    it("keeps self comments when includeSelf is true", () => {
      const c = {
        body: "Will rebase and address feedback in the next push.",
        user: { login: "alice", type: "User" as const },
      };
      assert.equal(classify(c, "alice", 20, true), "keep");
    });

    it("drops short LGTM-only bodies", () => {
      const c = {
        body: "LGTM!",
        user: { login: "bob", type: "User" as const },
      };
      assert.equal(classify(c, "alice", 20, false), "short");
    });

    it("keeps longer comments that merely start with 'Thanks'", () => {
      const c = {
        body: "Thanks for the patch! Now could you also handle the cancellation case in the loop above?",
        user: { login: "bob", type: "User" as const },
      };
      assert.equal(classify(c, "alice", 20, false), "keep");
    });

    it("drops short bodies", () => {
      const c = {
        body: "+1",
        user: { login: "bob", type: "User" as const },
      };
      assert.equal(classify(c, "alice", 20, false), "short");
    });

    it("drops quoted-only bodies", () => {
      const c = {
        body: "> something said earlier in the thread",
        user: { login: "bob", type: "User" as const },
      };
      assert.equal(classify(c, "alice", 20, false), "quoted");
    });

    it("keeps substantive review comments", () => {
      const c = {
        body: "Wrap this error with fmt.Errorf using %w so callers can errors.Is it.",
        user: { login: "carol", type: "User" as const },
      };
      assert.equal(classify(c, "alice", 20, false), "keep");
    });
  });
});

describe("LOW_SIGNAL", () => {
  const substantive =
    "Thanks for the patch! Could you also handle context cancellation in this loop?";

  it("matches canonical low-signal bodies", () => {
    const samples = ["LGTM!", "+1", "thanks", "TY.", "done", "fixed", "ack", "acknowledged."];
    for (const s of samples) {
      assert.equal(
        LOW_SIGNAL.some((re) => re.test(s)),
        true,
        `expected LOW_SIGNAL to match: ${s}`,
      );
    }
  });

  it("does not match longer substantive comments that begin with acknowledgement", () => {
    assert.equal(
      LOW_SIGNAL.some((re) => re.test(substantive)),
      false,
    );
  });

  it("classify keeps substantive acknowledgements when length passes minLength", () => {
    const c = {
      body: substantive,
      user: { login: "bob", type: "User" as const },
    };
    assert.equal(classify(c, "alice", 20, false), "keep");
  });

  it("classify still drops exact low-signal tokens even when minLength is permissive", () => {
    const c = {
      body: "LGTM!",
      user: { login: "bob", type: "User" as const },
    };
    assert.equal(classify(c, "alice", 1, false), "short");
  });
});

describe("filterPullRequestData", () => {
  it("processes the full fixture as expected", () => {
    const { kept, dropped } = filterPullRequestData(FIXTURE, DEFAULTS);

    // Kept: review #1002 (carol, ctx.Err) + inline #2001 (carol, fmt.Errorf)
    // + issue #3001 (eve, public API surface) = 3
    assert.equal(kept.length, 3);
    assert.deepEqual(kept.map((k) => k.user).sort(), ["carol", "carol", "eve"]);
    assert.equal(kept[0]!.pr, 42);

    // Dropped:
    //   bot:    #2004 (github-actions[bot])
    //   self:   #2003 (alice quoted, classified as self first) + #2005 (alice rebase msg)
    //   short:  review #1001 ("LGTM thanks!"), inline #2002 ("+1")
    // Note: empty-body review #1003 is filtered out before classify.
    assert.equal(dropped.bot, 1);
    assert.equal(dropped.self, 2);
    assert.equal(dropped.short, 2);
    assert.equal(dropped.quoted, 0);
  });

  it("includes issue comments by default", () => {
    const { kept } = filterPullRequestData(FIXTURE, DEFAULTS);
    // Adds eve's issue comment about public API surface -> total 3 kept.
    assert.equal(kept.length, 3);
    assert.ok(kept.some((k) => k.source === "issue" && k.user === "eve"));
  });

  it("includeSelf=true keeps alice's substantive comments", () => {
    const { kept } = filterPullRequestData(FIXTURE, {
      ...DEFAULTS,
      includeSelf: true,
    });
    // alice has 2 self comments: #2003 (quoted-only -> dropped) and #2005 (substantive -> kept).
    assert.ok(kept.some((k) => k.user === "alice" && k.body.includes('rebase')));
  });

});

describe("isAutomationBoilerplate", () => {
  it("detects Copilot CLI PR marker", () => {
    assert.equal(
      isAutomationBoilerplate("Some text <!-- #comment-cli-pr -->"),
      true,
    );
  });

  it("detects install-instructions marker", () => {
    assert.equal(
      isAutomationBoilerplate("<!-- install-instructions -->\n## Install"),
      true,
    );
  });

  it("returns false for normal review comments", () => {
    assert.equal(
      isAutomationBoilerplate("Please wrap this error with fmt.Errorf."),
      false,
    );
  });

  it("AUTOMATION_MARKERS is non-empty", () => {
    assert.ok(AUTOMATION_MARKERS.length > 0);
  });
});

describe("DEFAULT_BOT_LOGINS", () => {
  it("contains known automation accounts", () => {
    assert.ok(DEFAULT_BOT_LOGINS.has("azure-sdk"));
    assert.ok(DEFAULT_BOT_LOGINS.has("copilot-swe-agent"));
    assert.ok(DEFAULT_BOT_LOGINS.has("copilot-pull-request-reviewer"));
  });
});

describe("classify with automation markers", () => {
  it("drops bodies containing the Copilot CLI marker as bot", () => {
    const c = {
      body:
        "Auto-generated PR description with detail <!-- #comment-cli-pr -->",
      user: { login: "alice", type: "User" as const },
    };
    assert.equal(classify(c, "alice", 20, true), "bot");
  });
});

describe("inferKind", () => {
  it("review source is always 'summary'", () => {
    assert.equal(inferKind("review", "anything here"), "summary");
  });

  it("inline body starting with 'Fixed' is 'reply'", () => {
    assert.equal(inferKind("inline", "Fixed in abc123, thanks!"), "reply");
  });

  it("inline body starting with 'Addressed' is 'reply'", () => {
    assert.equal(inferKind("inline", "Addressed in the last commit."), "reply");
  });

  it("inline body starting with 'Good catch' is 'reply'", () => {
    assert.equal(inferKind("inline", "Good catch — pushed a fix."), "reply");
  });

  it("inline author-justification bodies are 'reply'", () => {
    const samples = [
      "Intentional. This PR removes the legacy product name.",
      "Not taken: adding an HTTP mock is too heavy for this fix.",
      "Accepted — great catch. Added a regression test.",
      "Agreed! Now -e creates the requested environment.",
      "You're right — removed the fallback.",
      "yes, added --version flag to support this.",
    ];
    for (const sample of samples) {
      assert.equal(inferKind("inline", sample), "reply", sample);
    }
  });

  it("substantive feedback is 'ask'", () => {
    assert.equal(
      inferKind("inline", "Please wrap this error using fmt.Errorf with %w."),
      "ask",
    );
  });

  it("AUTHOR_REPLY_PATTERNS is non-empty", () => {
    assert.ok(AUTHOR_REPLY_PATTERNS.length > 0);
  });
});

describe("shouldSkipPr", () => {
  const base: PullRequestData = {
    pr: {
      number: 100,
      title: "x",
      author: { login: "alice", type: "User" },
      url: "https://github.com/o/r/pull/100",
      state: "closed",
      mergedAt: "2026-05-15T12:00:00Z",
    },
    reviews: [],
    inline: [],
    issue: [],
  };

  it("returns false when no PR-level filters are set", () => {
    assert.equal(shouldSkipPr(base, { ...DEFAULTS }), false);
  });

  it("--since drops PRs merged before the date", () => {
    assert.equal(
      shouldSkipPr(base, { ...DEFAULTS, since: "2026-06-01" }),
      true,
    );
    assert.equal(
      shouldSkipPr(base, { ...DEFAULTS, since: "2026-05-01" }),
      false,
    );
  });

  it("--until drops PRs merged on/after the date", () => {
    assert.equal(
      shouldSkipPr(base, { ...DEFAULTS, until: "2026-05-15T12:00:00Z" }),
      true,
    );
    assert.equal(
      shouldSkipPr(base, { ...DEFAULTS, until: "2026-06-01" }),
      false,
    );
  });

  it("PR without mergedAt is skipped when --since is set", () => {
    const unmerged: PullRequestData = { ...base, pr: { ...base.pr, mergedAt: null } };
    assert.equal(
      shouldSkipPr(unmerged, { ...DEFAULTS, since: "2026-05-01" }),
      true,
    );
  });
});

describe("filterPullRequestData with new filters", () => {
  it("kept comments carry a kind field", () => {
    const { kept } = filterPullRequestData(FIXTURE, DEFAULTS);
    for (const c of kept) {
      assert.ok(["ask", "reply", "summary"].includes(c.kind));
    }
    // carol's review summary is 'summary'
    const review = kept.find((c) => c.source === "review");
    assert.equal(review?.kind, "summary");
    // carol's inline & eve's issue are 'ask' (no reply phrasing)
    const inline = kept.find((c) => c.source === "inline");
    assert.equal(inline?.kind, "ask");
  });

  it("--source inline restricts to inline comments and counts sourceFiltered", () => {
    const { kept, dropped } = filterPullRequestData(FIXTURE, {
      ...DEFAULTS,
      sources: new Set(["inline"]),
    });
    assert.ok(kept.every((k) => k.source === "inline"));
    assert.ok(dropped.sourceFiltered > 0);
  });

  it("--kind ask drops 'summary' and counts kindFiltered", () => {
    const { kept, dropped } = filterPullRequestData(FIXTURE, {
      ...DEFAULTS,
      kinds: new Set(["ask"]),
    });
    assert.ok(kept.every((k) => k.kind === "ask"));
    // The fixture's review-summary from carol gets dropped → kindFiltered++
    assert.ok(dropped.kindFiltered >= 1);
  });

  it("--since after PR mergedAt skips the PR wholesale", () => {
    const { kept, prSkipped } = filterPullRequestData(FIXTURE, {
      ...DEFAULTS,
      since: "2027-01-01",
    });
    assert.equal(kept.length, 0);
    assert.equal(prSkipped, true);
  });

  it("--max-body-length truncates long bodies with ellipsis", () => {
    const { kept } = filterPullRequestData(FIXTURE, {
      ...DEFAULTS,
      maxBodyLength: 30,
    });
    for (const c of kept) {
      assert.ok(c.body.length <= 31, `body too long: ${c.body.length}`);
      if (c.body.length === 31) assert.ok(c.body.endsWith("…"));
    }
  });

  it("drops comments authored by DEFAULT_BOT_LOGINS by default (programmatic call)", () => {
    // PR data: one substantive comment from a known automation
    // account that's not flagged as type:Bot by the API.
    const pullRequestData: PullRequestData = {
      pr: {
        number: 999,
        title: "Release",
        author: { login: "alice", type: "User" },
        url: "https://github.com/o/r/pull/999",
        state: "closed",
        mergedAt: "2026-06-01T00:00:00Z",
      },
      reviews: [],
      inline: [],
      issue: [
        {
          id: 4001,
          body: "Install the latest with `npm i pkg`. See the release notes for details.",
          created_at: "2026-06-01T00:01:00Z",
          user: { login: "azure-sdk", type: "User" },
        },
      ],
    };
    const { kept, dropped } = filterPullRequestData(pullRequestData, {
      ...DEFAULTS,
    });
    assert.equal(kept.length, 0);
    assert.equal(dropped.bot, 1);
  });

  it("defaultBots: false lets through comments from DEFAULT_BOT_LOGINS", () => {
    const pullRequestData: PullRequestData = {
      pr: {
        number: 999,
        title: "Release",
        author: { login: "alice", type: "User" },
        url: "https://github.com/o/r/pull/999",
        state: "closed",
        mergedAt: "2026-06-01T00:00:00Z",
      },
      reviews: [],
      inline: [],
      issue: [
        {
          id: 4001,
          body: "Install the latest with `npm i pkg`. See the release notes for details.",
          created_at: "2026-06-01T00:01:00Z",
          user: { login: "azure-sdk", type: "User" },
        },
      ],
    };
    const { kept } = filterPullRequestData(pullRequestData, {
      ...DEFAULTS,
      defaultBots: false,
    });
    assert.equal(kept.length, 1);
    assert.equal(kept[0]!.user, "azure-sdk");
  });

  it("drops bodies containing the Copilot CLI marker as bot through the full pipeline", () => {
    const pullRequestData: PullRequestData = {
      pr: {
        number: 999,
        title: "Auto",
        author: { login: "alice", type: "User" },
        url: "https://github.com/o/r/pull/999",
        state: "closed",
        mergedAt: "2026-06-01T00:00:00Z",
      },
      reviews: [],
      inline: [],
      issue: [
        {
          id: 4002,
          body:
            "This PR was generated by an automation tool. <!-- #comment-cli-pr -->",
          created_at: "2026-06-01T00:01:00Z",
          user: { login: "bob", type: "User" },
        },
      ],
    };
    const { kept, dropped } = filterPullRequestData(pullRequestData, {
      ...DEFAULTS,
    });
    assert.equal(kept.length, 0);
    assert.equal(dropped.bot, 1);
  });

  it("tags author-reply-shaped bodies as kind 'reply'", () => {
    const pullRequestData: PullRequestData = {
      pr: {
        number: 999,
        title: "x",
        author: { login: "alice", type: "User" },
        url: "https://github.com/o/r/pull/999",
        state: "closed",
        mergedAt: "2026-06-01T00:00:00Z",
      },
      reviews: [],
      inline: [
        {
          id: 5001,
          path: "main.go",
          line: 1,
          body: "Fixed in the latest commit, thanks for the catch!",
          user: { login: "alice", type: "User" },
        },
      ],
      issue: [],
    };
    const { kept } = filterPullRequestData(pullRequestData, {
      ...DEFAULTS,
      includeSelf: true,
    });
    assert.equal(kept.length, 1);
    assert.equal(kept[0]!.kind, "reply");
  });

  it("populates comment_url with the per-comment deep link", () => {
    const { kept } = filterPullRequestData(FIXTURE, DEFAULTS);
    const inline = kept.find((c) => c.source === "inline");
    assert.ok(inline?.comment_url?.endsWith("#discussion_r2001"));
    const review = kept.find((c) => c.source === "review");
    assert.ok(review?.comment_url?.endsWith("#pullrequestreview-1002"));
    const issue = kept.find((c) => c.source === "issue");
    assert.ok(issue?.comment_url?.includes("/issues/42#issuecomment-3001"));
  });
});

describe("parseArgs with new flags", () => {
  const sampleFile = path.join(__dirname, "fixtures", "pr-sample.json");

  it("--no-default-bots disables the default bot exclude list", () => {
    const opts = parseArgs([sampleFile, "--no-default-bots"]);
    assert.equal(opts.defaultBots, false);
  });

  it("default behavior leaves defaultBots=true", () => {
    const opts = parseArgs([sampleFile]);
    assert.equal(opts.defaultBots, true);
  });

  it("--source parses comma-separated sources", () => {
    const opts = parseArgs([
      sampleFile,
      "--source",
      "inline,issue",
    ]);
    assert.deepEqual([...(opts.sources ?? [])].sort(), ["inline", "issue"]);
  });

  it("--kind parses comma-separated kinds", () => {
    const opts = parseArgs([sampleFile, "--kind", "ask"]);
    assert.deepEqual([...(opts.kinds ?? [])], ["ask"]);
  });

  it("--since and --until are parsed as ISO dates", () => {
    const opts = parseArgs([
      sampleFile,
      "--since",
      "2026-05-08",
      "--until",
      "2026-06-08",
    ]);
    assert.equal(opts.since, "2026-05-08");
    assert.equal(opts.until, "2026-06-08");
  });

  it("--max-body-length validates positive integer", () => {
    const opts = parseArgs([
      sampleFile,
      "--max-body-length",
      "100",
    ]);
    assert.equal(opts.maxBodyLength, 100);
  });
});
