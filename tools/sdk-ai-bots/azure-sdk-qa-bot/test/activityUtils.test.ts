import { Activity, TurnContext } from 'botbuilder';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { sendActivityWithRetry, updateActivityWithRetry } from '../src/activityUtils.js';

describe('activityUtils.sendActivityWithRetry', () => {
  let mockContext: Partial<TurnContext>;
  let mockRestError = {
    name: "RestError",
    message: "Too Many Messages",
    statusCode: 429,
    details: { rawBody: "<html>...</html>" },  // throw-site dependent,
    response: {
        headers: { "retry-after": "3" },
        status: 429
    },
    stack: "RestError: Bad Request ..."
  }

  it('success send activity', async () => {
    // Mock TurnContext
    mockContext = {
      activity: {
        conversation: { id: 'test-conversation' },
        id: 'test-activity',
      },
      sendActivity: vi.fn().mockResolvedValue({ id: 'test-resource' }),
      updateActivity: vi.fn().mockResolvedValue({ id: 'test-response' }),
    } as any;

    const result = await sendActivityWithRetry(mockContext as TurnContext, "Test activity");
    expect(result).toStrictEqual({id: 'test-resource'});
  });

  it('429 exception once when send activity', async () => {
    let numOfCalls = 0;
    // Mock TurnContext
    mockContext = {
      activity: {
        conversation: { id: 'test-conversation' },
        id: 'test-activity',
      },
      sendActivity: vi.fn().mockImplementation(() => {
        if (numOfCalls == 0) {
            numOfCalls++;
            throw mockRestError;
        }
        numOfCalls++
        return { id: 'test-resource' };
      }),
      updateActivity: vi.fn().mockResolvedValue({ id: 'test-response' }),
    } as any;

    const result = await sendActivityWithRetry(mockContext as TurnContext, "Test activity");
    expect(result).toStrictEqual({id: 'test-resource'});
    expect(numOfCalls).equals(2);
  });

  it('429 exception always when send activity', async () => {
    let numOfCalls = 0;
    // Mock TurnContext
    mockContext = {
      activity: {
        conversation: { id: 'test-conversation' },
        id: 'test-activity',
      },
      sendActivity: vi.fn().mockImplementation(() => {
        numOfCalls++
        throw mockRestError;
      }),
      updateActivity: vi.fn().mockResolvedValue({ id: 'test-response' }),
    } as any;

    await expect(sendActivityWithRetry(mockContext as TurnContext, "Test activity")).rejects.toThrow("Too Many Messages");
    expect(numOfCalls).equals(6);
  }, 20000);
});

describe('activityUtils.updateActivityWithRetry', () => {
  let mockContext: Partial<TurnContext>;
  let mockRestError = {
    name: "RestError",
    message: "Too Many Messages",
    statusCode: 429,
    details: { rawBody: "<html>...</html>" },  // throw-site dependent,
    response: {
        headers: { "retry-after": "3" },
        status: 429
    },
    stack: "RestError: Bad Request ..."
  }

  let updateActivity:Partial<Activity> = {
    type: 'message',
    id: 'test-resource',
    text: "update message",
  } as any;

  it('success send activity', async () => {
    // Mock TurnContext
    mockContext = {
      activity: {
        conversation: { id: 'test-conversation' },
        id: 'test-activity',
      },
      sendActivity: vi.fn().mockResolvedValue({ id: 'test-resource' }),
      updateActivity: vi.fn().mockResolvedValue({ id: 'test-resource' }),
    } as any;

    const result = await updateActivityWithRetry(mockContext as TurnContext, updateActivity);
    expect(result).toStrictEqual({id: 'test-resource'});
  });

  it('429 exception once when send activity', async () => {
    let numOfCalls = 0;
    // Mock TurnContext
    mockContext = {
      activity: {
        conversation: { id: 'test-conversation' },
        id: 'test-activity',
      },
      sendActivity: vi.fn().mockResolvedValue({ id: 'test-resource' }),
      updateActivity: vi.fn().mockImplementation(() => {
        if (numOfCalls == 0) {
            numOfCalls++;
            throw mockRestError;
        }
        numOfCalls++
        return { id: 'test-resource' };
      }),
    } as any;

    const result = await updateActivityWithRetry(mockContext as TurnContext, updateActivity);
    expect(result).toStrictEqual({id: 'test-resource'});
    expect(numOfCalls).equals(2);
  });

  it('429 exception always when send activity', async () => {
    let numOfCalls = 0;
    // Mock TurnContext
    mockContext = {
      activity: {
        conversation: { id: 'test-conversation' },
        id: 'test-activity',
      },
      sendActivity: vi.fn().mockResolvedValue({ id: 'test-resource' }),
      updateActivity: vi.fn().mockImplementation(() => {
        numOfCalls++
        throw mockRestError;
      }),
    } as any;

    await expect(updateActivityWithRetry(mockContext as TurnContext, updateActivity)).rejects.toThrow("Too Many Messages");
    expect(numOfCalls).equals(6);
  }, 20000);
});
