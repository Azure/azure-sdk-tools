import { app, HttpRequest, HttpResponseInit, InvocationContext } from "@azure/functions";
import { ChatMessage} from '@microsoft/microsoft-graph-types';
import { Activity } from 'botframework-schema';

export class ActivityConverter {

  constructor() {}

  ConvertToActivity(chatMessage: ChatMessage): Activity {
    // extract tenantID from query parameters of chatMessage.webUrl
    const url = new URL(chatMessage.webUrl || '');
    const tenantId = url.searchParams.get('tenantId') || '';
    // Create a new Activity object
    const activity: Activity = {
      // Basic message properties
      label: chatMessage.subject || '',
      valueType: '',
      listenFor: [],
      type: 'message',
      id: chatMessage.id || '',
      timestamp: new Date(chatMessage.createdDateTime || new Date()),
      localTimestamp: new Date(chatMessage.createdDateTime || new Date()),

      // Channel information
      channelId: 'msteams',
      serviceUrl: 'https://smba.trafficmanager.net/apac/' + tenantId,
      
      // Text content
      text: chatMessage.body?.content || '',
      textFormat: 'plain',
      
      // Attachments for HTML content
      attachments: chatMessage.body?.contentType === 'html' ? [
        {
          contentType: 'text/html',
          content: chatMessage.body.content
        }
      ] : [],
      
      // Sender information
      from: {
        id: chatMessage.from?.user?.id || '',
        name: chatMessage.from?.user?.displayName || '',
        aadObjectId: chatMessage.from?.user?.id || '',
        role: 'user'
      },
      
      // Conversation information
      conversation: {
        name: chatMessage.subject || '',
        isGroup: true,
        conversationType: 'channel',
        tenantId: tenantId,
        id: (chatMessage.channelIdentity?.channelId || '') + 
            ';messageid=' + (chatMessage.id || '')
      },
      
      // Recipient information (bot)
      recipient: {
        id: '28:189f38c2-e2a7-433d-944e-b1166e5402c2',
        name: 'Azure SDK Q&A Bot'
      },
      
      // Entities like mentions and client info
      entities: [
        // Add mention entities
        ...(chatMessage.mentions?.map(mention => ({
          mentioned: {
            id: mention.mentioned?.application?.id ? 
                '28:' + mention.mentioned.application.id : '',
            name: mention.mentioned?.application?.displayName || ''
          },
          text: `<at>${mention.mentionText}</at>`,
          type: 'mention'
        })) || []),
        
        // Add client info entity
        {
          locale: chatMessage.locale,
          country: 'US',
          platform: 'Windows',
          timezone: 'Asia/Shanghai',
          type: 'clientInfo'
        }
      ],
      
      // Teams specific channel data
      channelData: {
        teamsChannelId: chatMessage.channelIdentity?.channelId || '',
        teamsTeamId: chatMessage.channelIdentity?.teamId || '',
        channel: {
          id: chatMessage.channelIdentity?.channelId || ''
        },
        tenant: {
          id: tenantId
        }
      },
      
      // Locale information
      locale: chatMessage.locale || 'en-US',
      localTimezone: 'Asia/Shanghai',
      callerId: 'urn:botframework:azure'
    };
    
    return activity;
  }
}

app.post('convertActivity', {
    authLevel: 'anonymous',
    handler: async (request: HttpRequest, context: InvocationContext): Promise<HttpResponseInit> => {
        context.log(`Http function processed request for url "${request.url}"`);

        const activity = await request.json();
        const activityConverter = new ActivityConverter();
        const convertedActivity = activityConverter.ConvertToActivity(activity);

        return { jsonBody: convertedActivity };
    }
})