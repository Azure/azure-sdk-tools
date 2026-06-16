import { describe, it } from "vitest";
import assert from "node:assert/strict";

import {
  formatElapsed,
  isBot,
  isHumanUser,
  parsePositiveInt,
} from "../scripts/utils.ts";
import type { User } from "../scripts/types.ts";

function fakeUser(login: string, type: "User" | "Bot" = "User"): User {
  return { login, type };
}

describe("parsePositiveInt", () => {
  it("accepts positive integers", () => {
    assert.equal(parsePositiveInt("5", "--top-files"), 5);
  });

  it("throws on zero/negative/non-numeric", () => {
    assert.throws(() => parsePositiveInt("0", "--top-files"), {
      message: /must be a positive integer/,
    });
    assert.throws(() => parsePositiveInt("-2", "--top-files"), {
      message: /must be a positive integer/,
    });
    assert.throws(() => parsePositiveInt("abc", "--top-files"), {
      message: /must be a positive integer/,
    });
  });
});

describe("isBot", () => {
  it("detects type=Bot", () => {
    assert.equal(isBot(fakeUser("x", "Bot")), true);
  });

  it("detects [bot] suffix", () => {
    assert.equal(isBot(fakeUser("dependabot[bot]", "User")), true);
  });

  it("returns false for humans and null", () => {
    assert.equal(isBot(fakeUser("alice", "User")), false);
    assert.equal(isBot(null), false);
  });
});

describe("isHumanUser", () => {
  it("returns true for humans", () => {
    assert.equal(isHumanUser(fakeUser("alice", "User")), true);
  });

  it("returns false for bots", () => {
    assert.equal(isHumanUser(fakeUser("x", "Bot")), false);
    assert.equal(isHumanUser(fakeUser("dependabot[bot]", "User")), false);
  });

  it("returns false for null", () => {
    assert.equal(isHumanUser(null), false);
  });
});

describe("formatElapsed", () => {
  it("formats milliseconds as MM:SS", () => {
    assert.equal(formatElapsed(0), "00:00");
    assert.equal(formatElapsed(5000), "00:05");
    assert.equal(formatElapsed(65000), "01:05");
    assert.equal(formatElapsed(3665000), "61:05");
  });
});
