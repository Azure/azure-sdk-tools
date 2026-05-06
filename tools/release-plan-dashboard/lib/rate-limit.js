/**
 * Simple in-memory sliding-window rate limiter middleware.
 * Tracks request counts per session within a time window.
 */
function createRateLimiter({ windowMs = 60 * 1000, maxRequests = 30 } = {}) {
  const hits = new Map(); // key -> [timestamps]

  // Periodic cleanup to avoid memory growth from expired entries
  const cleanupInterval = setInterval(() => {
    const now = Date.now();
    for (const [key, timestamps] of hits) {
      const valid = timestamps.filter(t => now - t < windowMs);
      if (valid.length === 0) hits.delete(key);
      else hits.set(key, valid);
    }
  }, windowMs * 2);
  cleanupInterval.unref();

  return function rateLimiter(req, res, next) {
    const key = (req.session && req.session.user && req.session.user.login) || req.ip || "anon";
    const now = Date.now();
    const timestamps = (hits.get(key) || []).filter(t => now - t < windowMs);

    if (timestamps.length >= maxRequests) {
      const retryAfter = Math.ceil((timestamps[0] + windowMs - now) / 1000);
      res.set("Retry-After", String(retryAfter));
      return res.status(429).json({ error: "Too many requests. Please try again later." });
    }

    timestamps.push(now);
    hits.set(key, timestamps);
    next();
  };
}

export { createRateLimiter };
