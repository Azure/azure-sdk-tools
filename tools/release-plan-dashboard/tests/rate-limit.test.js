import { describe, test, expect, vi } from "vitest";
import { createRateLimiter } from "../lib/rate-limit.js";

function mockReq(user, ip) {
  return {
    user: user ? { login: user, objectId: `obj-${user}` } : null,
    ip: ip || "127.0.0.1",
  };
}

function mockRes() {
  const res = {
    _status: null,
    _json: null,
    _headers: {},
    status(code) {
      res._status = code;
      return res;
    },
    json(data) {
      res._json = data;
      return res;
    },
    set(key, val) {
      res._headers[key] = val;
      return res;
    },
  };
  return res;
}

describe("rate-limit module", () => {
  test("exports createRateLimiter function", () => {
    expect(typeof createRateLimiter).toBe("function");
  });

  test("allows requests under the limit", () => {
    const limiter = createRateLimiter({ windowMs: 60000, maxRequests: 5 });
    const req = mockReq("testuser");
    const res = mockRes();
    let called = false;
    const next = () => {
      called = true;
    };

    limiter(req, res, next);
    expect(called).toBe(true);
    expect(res._status).toBeNull();
  });

  test("blocks requests over the limit with 429", () => {
    const limiter = createRateLimiter({ windowMs: 60000, maxRequests: 3 });
    const req = mockReq("blockuser");
    const next = vi.fn();
    for (let i = 0; i < 3; i++) {
      const res = mockRes();
      limiter(req, res, next);
    }
    expect(next).toHaveBeenCalledTimes(3);

    // 4th request should be blocked
    const res4 = mockRes();
    limiter(req, res4, next);
    expect(next).toHaveBeenCalledTimes(3); // not called again
    expect(res4._status).toBe(429);
    expect(res4._json.error).toContain("Too many requests");
    expect(res4._headers["Retry-After"]).toBeDefined();
  });

  test("uses user login as key when available", () => {
    const limiter = createRateLimiter({ windowMs: 60000, maxRequests: 2 });
    const next = vi.fn();

    // Different users should have separate counters
    const req1 = mockReq("user1");
    const req2 = mockReq("user2");

    limiter(req1, mockRes(), next);
    limiter(req1, mockRes(), next);
    limiter(req2, mockRes(), next);

    expect(next).toHaveBeenCalledTimes(3);

    // user1 is now at limit
    limiter(req1, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(3); // blocked

    // user2 still has room
    limiter(req2, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(4); // allowed
  });

  test("falls back to IP when no session user", () => {
    const limiter = createRateLimiter({ windowMs: 60000, maxRequests: 2 });
    const next = vi.fn();

    const req1 = mockReq(null, "10.0.0.1");
    const req2 = mockReq(null, "10.0.0.2");

    limiter(req1, mockRes(), next);
    limiter(req1, mockRes(), next);
    limiter(req2, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(3);

    // Same IP at limit
    limiter(req1, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(3);
  });

  test("sliding window expires old requests", () => {
    vi.useFakeTimers();
    const limiter = createRateLimiter({ windowMs: 1000, maxRequests: 2 });
    const req = mockReq("timeruser");
    const next = vi.fn();

    limiter(req, mockRes(), next);
    limiter(req, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(2);

    // Should be blocked
    limiter(req, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(2);

    // Advance time past window
    vi.advanceTimersByTime(1100);

    // Should be allowed again
    limiter(req, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(3);

    vi.useRealTimers();
  });

  test("uses default options when none provided", () => {
    const limiter = createRateLimiter();
    expect(typeof limiter).toBe("function");

    const req = mockReq("defaultuser");
    const next = vi.fn();
    limiter(req, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(1);
  });

  test("cleanup interval removes expired entries", () => {
    vi.useFakeTimers();
    const limiter = createRateLimiter({ windowMs: 1000, maxRequests: 5 });
    const req = mockReq("cleanupuser");
    const next = vi.fn();

    // Make a request at t=0
    limiter(req, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(1);

    // Advance to t=1500 — first entry is now expired
    vi.advanceTimersByTime(1500);

    // Make another request at t=1500 — this is within current window
    limiter(req, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(2);

    // Advance to t=2000 — cleanup fires (windowMs*2 = 2000)
    // At cleanup: t=0 entry is expired (2000-0=2000 > 1000), t=1500 is valid (2000-1500=500 < 1000)
    // This exercises the else branch (line 14): valid.length > 0 → hits.set(key, valid)
    vi.advanceTimersByTime(500);

    // The t=1500 entry should still count
    // Make requests to fill up — we have 1 valid entry, so 4 more should work
    for (let i = 0; i < 4; i++) {
      limiter(req, mockRes(), next);
    }
    expect(next).toHaveBeenCalledTimes(6);

    // 5th should be blocked (1 existing + 4 new = 5 = maxRequests)
    limiter(req, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(6);

    vi.useRealTimers();
  });

  test("cleanup interval deletes fully expired entries", () => {
    vi.useFakeTimers();
    const limiter = createRateLimiter({ windowMs: 100, maxRequests: 1 });
    const req = mockReq("expireuser");
    const next = vi.fn();

    limiter(req, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(1);

    // Advance past cleanup interval (200ms) — all entries expired
    vi.advanceTimersByTime(200);

    // Should be allowed (expired entries cleaned)
    limiter(req, mockRes(), next);
    expect(next).toHaveBeenCalledTimes(2);

    vi.useRealTimers();
  });
});
