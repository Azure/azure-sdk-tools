import express, { ErrorRequestHandler } from 'express';
import { AddressInfo } from 'node:net';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { messageBodyParser } from '../src/input/messageBodyParser.js';

describe('Teams message body size', () => {
  let server: ReturnType<express.Express['listen']>;
  let endpoint: string;

  beforeAll(async () => {
    const app = express();
    app.use(messageBodyParser);
    app.post('/api/messages', (req, res) => {
      res.json({ imageCount: req.body.attachments.length });
    });
    const handleError: ErrorRequestHandler = (error, _req, res, _next) => {
      res.status(error.status ?? 500).end();
    };
    app.use(handleError);
    await new Promise<void>((resolve, reject) => {
      server = app.listen(0, '127.0.0.1', (error) => error ? reject(error) : resolve());
    });
    endpoint = `http://127.0.0.1:${(server.address() as AddressInfo).port}/api/messages`;
  });

  afterAll(async () => {
    await new Promise<void>((resolve, reject) => {
      server.close((error) => error ? reject(error) : resolve());
    });
  });

  it('accepts four maximum-size base64 images', async () => {
    const encoded = 'A'.repeat(4 * Math.ceil(5 * 1024 * 1024 / 3));
    const attachments = Array.from({ length: 4 }, () => ({
      contentType: 'image/*',
      contentUrl: `data:image/png;base64,${encoded}`,
    }));
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ attachments }),
    });
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ imageCount: 4 });
  }, 10000);

  it('rejects request bodies beyond the transport limit', async () => {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text: 'A'.repeat(30 * 1024 * 1024) }),
    });
    expect(response.status).toBe(413);
  }, 10000);
});
