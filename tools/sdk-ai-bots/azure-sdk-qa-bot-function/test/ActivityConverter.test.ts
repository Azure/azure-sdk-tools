import { ChatMessage } from '@microsoft/microsoft-graph-types';
import { describe, expect, it } from 'vitest';

import { ActivityConverter } from '../src/functions/ActivityConverter';

describe('ActivityConverter', () => {
  it('uses the global Teams connector and configured bot identity', () => {
    const message = {
      id: 'message-id',
      replyToId: 'root-message-id',
      createdDateTime: '2026-08-25T10:48:36.019Z',
      webUrl: 'https://teams.microsoft.com/l/message/thread/message-id?tenantId=tenant-id',
      body: {
        contentType: 'html',
        content: '<p>what is TypeSpec?</p>'
      },
      channelIdentity: {
        channelId: 'channel-id',
        teamId: 'team-id'
      },
      locale: 'en-us'
    } as ChatMessage;

    const activity = new ActivityConverter('bot-id').ConvertToActivity(message);

    expect(activity.serviceUrl).toBe('https://smba.trafficmanager.net/teams/');
    expect(activity.recipient?.id).toBe('28:bot-id');
  });
});