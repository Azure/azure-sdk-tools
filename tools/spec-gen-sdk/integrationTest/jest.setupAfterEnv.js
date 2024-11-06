jest.setTimeout(3600_000);

// Avoid color output in snapshot
process.env.FORCE_COLOR = '0' // See chalk which is used by autorest
process.env.NO_COLOR = '1'