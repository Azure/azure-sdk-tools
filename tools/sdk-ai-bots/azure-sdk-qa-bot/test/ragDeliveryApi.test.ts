import { beforeEach, describe, expect, it, vi } from 'vitest';
import axios from 'axios';
import { getRAGReply } from '../src/backend/rag.js';

vi.mock('axios', () => ({ default: { get: vi.fn(), post: vi.fn() } }));
vi.mock('../src/config/config.js', () => ({ ragApiPaths: { completion: '/completion' } }));
vi.mock('../src/logging/logger.js', () => ({ logger: { info: vi.fn(), warn: vi.fn(), error: vi.fn() } }));

describe('completion API', () => {
  const options = { endpoint: 'https://backend' };
  const request = {
    tenant_id: 'tenant',
    message: { role: 'user' as const, content: 'Question' },
    conversation_id: 'channel;messageid=root', conversation_type: 'teams_channel',
  };
  beforeEach(() => vi.clearAllMocks());

  it('receives notification permission with one completion request and no configuration request', async () => {
    const response = { id: 'answer', answer: 'Answer', has_result: true, notify_experts: true };
    vi.mocked(axios.post).mockResolvedValue({ status: 200, data: response });
    await expect(getRAGReply(request, options, {})).resolves.toEqual(response);
    expect(axios.post).toHaveBeenCalledExactlyOnceWith('https://backend/completion', request, expect.anything());
    expect(axios.get).not.toHaveBeenCalled();
  });

  it('does not retry a lost generation response or make a delivery acknowledgement request', async () => {
    vi.mocked(axios.post).mockRejectedValue(new Error('completion response timeout'));
    await expect(getRAGReply(request, options, {})).resolves.toBeUndefined();
    expect(axios.post).toHaveBeenCalledOnce();
  });
});
