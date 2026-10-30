import axios from 'axios';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { getRAGReply, CompletionRequestPayload } from '../src/backend/rag.js';
import { logger } from '../src/logging/logger.js';

vi.mock('axios', () => ({
  default: {
    post: vi.fn(),
    isAxiosError: (error: { isAxiosError?: boolean }) => error.isAxiosError === true,
  },
}));
vi.mock('../src/config/config.js', () => ({
  ragApiPaths: { completion: '/agent/chat' },
}));
vi.mock('../src/logging/logger.js', () => ({
  logger: { info: vi.fn(), warn: vi.fn(), error: vi.fn() },
}));

describe('RAG inline image payload', () => {
  const image = 'data:image/png;base64,iVBORw0KGgo=';
  const payload: CompletionRequestPayload = {
    tenant_id: 'test',
    message: { role: 'user', content: 'What error is visible?' },
    additional_infos: [{ type: 'image', content: '', link: image }],
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('sends the original image bytes but omits them from payload logs', async () => {
    vi.mocked(axios.post).mockResolvedValue({
      status: 200,
      data: { id: 'reply', answer: 'The screenshot shows an error.', has_result: true },
    });
    await getRAGReply(payload, { endpoint: 'https://backend.example' }, {});
    expect(axios.post).toHaveBeenCalledWith(
      'https://backend.example/agent/chat', payload, expect.any(Object),
    );
    expect(payload.additional_infos[0].link).toBe(image);
    expect(JSON.stringify(vi.mocked(logger.info).mock.calls)).not.toContain(image);
  });

  it('does not log the Axios request body when an image request fails', async () => {
    vi.mocked(axios.post).mockRejectedValue({
      isAxiosError: true,
      name: 'AxiosError',
      message: 'Request failed',
      response: { status: 500 },
      config: { data: JSON.stringify(payload) },
    });
    expect(await getRAGReply(payload, { endpoint: 'https://backend.example' }, {})).toBeUndefined();
    expect(logger.warn).toHaveBeenCalled();
    expect(JSON.stringify(vi.mocked(logger.warn).mock.calls)).not.toContain(image);
  });
});
