import express from 'express';

// Four 5 MiB images expand to about 27 MiB when base64-encoded.
export const messageBodyParser = express.json({ limit: '30mb' });
