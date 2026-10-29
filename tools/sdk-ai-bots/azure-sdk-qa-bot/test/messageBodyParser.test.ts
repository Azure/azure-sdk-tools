import express, { ErrorRequestHandler } from 'express';
import { Activity, CloudAdapter } from 'botbuilder';
import { AddressInfo } from 'node:net';
import { afterAll, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { messageBodyParser } from '../src/input/messageBodyParser.js';

class TransportTestAdapter extends CloudAdapter {
  public readonly receivedActivity = vi.fn<(activity: Activity) => void>();

  protected override async processActivity(_authorization: unknown, activity: Activity): Promise<undefined> {
    // Keep authentication and reply generation outside these HTTP transport tests.
    this.receivedActivity(activity);
    return undefined;
  }
}

describe('Teams message HTTP transport', () => {
  let server: ReturnType<express.Express['listen']>;
  let endpoint: string;
  const adapter = new TransportTestAdapter();
  const processActivity = adapter.receivedActivity;

  beforeEach(() => {
    processActivity.mockClear();
  });

  beforeAll(async () => {
    const app = express();
    app.use(messageBodyParser);
    app.post('/api/messages', async (req, res, next) => {
      try {
        await adapter.process(req, res, async () => {});
      } catch (error) {
        next(error);
      }
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

  it('does not accept an empty Logic Apps chunked-upload negotiation as an activity', async () => {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: {
        'x-ms-transfer-mode': 'chunked',
        'x-ms-content-length': '169000',
      },
    });
    expect(response.status).toBe(400);
    expect(await response.text()).toBe(
      '`req.body` not an object, make sure you are using middleware to parse incoming requests.',
    );
    expect(processActivity).not.toHaveBeenCalled();
  });

  it('passes a complete text-only JSON activity to Bot Framework processing', async () => {
    const activity = { type: 'message', text: 'Why did CI fail?' };
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(activity),
    });
    expect(response.status).toBe(200);
    expect(processActivity).toHaveBeenCalledExactlyOnceWith(activity);
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
      body: JSON.stringify({ type: 'message', attachments }),
    });
    expect(response.status).toBe(200);
    expect(processActivity).toHaveBeenCalledOnce();
    const activity = processActivity.mock.calls[0][0];
    expect(activity.type).toBe('message');
    expect(activity.attachments).toHaveLength(4);
    expect(activity.attachments.map((attachment) => attachment.contentUrl.length)).toEqual(
      attachments.map((attachment) => attachment.contentUrl.length),
    );
  }, 10000);

  it('rejects request bodies beyond the transport limit', async () => {
    const response = await fetch(endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text: 'A'.repeat(30 * 1024 * 1024) }),
    });
    expect(response.status).toBe(413);
    expect(processActivity).not.toHaveBeenCalled();
  }, 10000);
});
