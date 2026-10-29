import { readFileSync } from 'node:fs';
import { validateAndFixActivity } from 'botbuilder/lib/activityValidator.js';
import { describe, expect, it } from 'vitest';

const template = JSON.parse(readFileSync(
  new URL('../../azure-sdk-qa-bot-agent/pipelines/logicapp/template.json', import.meta.url),
  'utf8',
));
const replyActions = template.resources[0].properties.definition.actions.For_each.actions
  .Thread_Message_From_User.actions.Need_Reply.actions.Should_Reply.actions;
const actions = replyActions.Resolve_image_bytes.actions;

const graphImage = 'https://graph.microsoft.com/v1.0/teams/t/channels/c/messages/m/hostedContents/i/$value';
const png = 'iVBORw0KGgo=';
const dataUri = `data:image/png;base64,${png}`;

interface Attachment {
  contentType: string;
  contentUrl?: string;
  content?: string;
}

interface DownloadResult {
  url: string;
  body: Record<string, string> | null;
}

function prepare(attachments: Attachment[], results: (DownloadResult | null)[]) {
  const run = new Function('workflowContext', actions.Prepare_activity_images.inputs.code);
  return run({
    actions: {
      convertActivity: { outputs: { body: {
        type: 'message',
        id: 'test-message',
        channelId: 'msteams',
        serviceUrl: 'https://smba.trafficmanager.net/apac/test-tenant',
        from: { id: 'test-user', name: 'Test User' },
        recipient: { id: 'test-bot', name: 'Test Bot' },
        conversation: { id: 'test-channel;messageid=test-message', conversationType: 'channel' },
        text: 'Why did CI fail?',
        attachments,
      } } },
      Image_download_results: { outputs: results },
    },
  });
}

function downloaded(
  url = graphImage,
  body: Record<string, string> = { '$content-type': 'image/png', '$content': png },
): DownloadResult {
  return { url, body };
}

function serializedBotRequest(activity: unknown) {
  const actionOutputs: Record<string, { body: unknown }> = {
    Prepare_activity_images: { body: activity },
  };
  const resolve = (expression: string): unknown => {
    const match = /^@(body|outputs)\('([^']+)'\)$/.exec(expression);
    if (!match || !actionOutputs[match[2]]) {
      throw new Error(`Unsupported activity expression: ${expression}`);
    }
    const output = actionOutputs[match[2]];
    return match[1] === 'body' ? output.body : output;
  };
  const invokeActions = replyActions.Invoke_bot.actions;
  actionOutputs.Validate_prepared_activity = {
    body: resolve(invokeActions.Validate_prepared_activity.inputs.content),
  };
  return JSON.parse(JSON.stringify(resolve(invokeActions.Invoke_QA_Bot.inputs.body)));
}

describe('Logic App connector image forwarding', () => {
  it.each([false, true])('serializes a valid Bot Framework activity (with image: %s)', (withImage) => {
    const activity = prepare(
      withImage ? [{ contentType: 'image/*', contentUrl: graphImage }] : [],
      withImage ? [downloaded()] : [null, null, null, null],
    );
    const payload = serializedBotRequest(activity);
    const validated = validateAndFixActivity(payload);
    expect(validated.type).toBe('message');
    expect(payload).not.toHaveProperty('body');
    expect(validated.id).toBe('test-message');
    expect(validated.channelId).toBe('msteams');
    expect(validated.serviceUrl).toBe(activity.serviceUrl);
    expect(validated.from).toEqual(activity.from);
    expect(validated.recipient).toEqual(activity.recipient);
    expect(validated.conversation.id).toBe('test-channel;messageid=test-message');
    expect(validated.attachments).toEqual(activity.attachments);
    if (withImage) {
      expect(validated.attachments?.[0].contentUrl).toBe(dataUri);
    }
  });

  it('uses the verified Teams HTTP action headers and existing connection', () => {
    for (let slot = 1; slot <= 4; slot++) {
      const download = actions[`Download_image_${slot}_if_present`].actions[`Download_Graph_image_${slot}`];
      expect(download.type).toBe('ApiConnection');
      expect(download.inputs.path).toBe('/httprequest');
      expect(download.inputs.method).toBe('post');
      expect(download.inputs.headers.Method).toBe('GET');
      expect(download.inputs.headers.Uri).toBe(`@body('Filter_graph_hosted_images')[${slot - 1}]['contentUrl']`);
      expect(download.inputs.host.connection.name).toBe("@parameters('$connections')['teams']['connectionId']");
      expect(download.inputs.retryPolicy.type).toBe('none');
      expect(download.runtimeConfiguration.secureData.properties).toEqual(['outputs']);
    }
    expect(actions.Prepare_activity_images.inputs.code.length).toBeLessThanOrEqual(1024);
  });

  it('limits downloads to the first four image candidates without shared variables', () => {
    expect(actions.Filter_graph_hosted_images.inputs.from).toBe("@take(body('Filter_image_attachments'), 4)");
    expect(actions.Filter_graph_hosted_images.inputs.where).toContain('https://graph.microsoft.com/');
    expect(actions.Filter_graph_hosted_images.inputs.where).toContain('/hostedContents/');
    for (let slot = 1; slot <= 4; slot++) {
      const condition = actions[`Download_image_${slot}_if_present`];
      expect(condition.type).toBe('If');
      expect(condition.expression).toBe(`@greater(length(body('Filter_graph_hosted_images')), ${slot - 1})`);
      expect(condition.runAfter).toEqual({ Filter_graph_hosted_images: ['Succeeded'] });
      const capture = condition.actions[`Capture_downloaded_image_${slot}`];
      expect(capture.runAfter).toEqual({ [`Download_Graph_image_${slot}`]: ['Succeeded'] });
      expect(capture.runtimeConfiguration.secureData.properties).toEqual(['inputs']);
      expect(capture.inputs.url).toBe(`@body('Filter_graph_hosted_images')[${slot - 1}]['contentUrl']`);
      expect(capture.inputs.body).toBe(
        `@if(lessOrEquals(length(coalesce(body('Download_Graph_image_${slot}')?['$content'], '')), mul(4, div(add(parameters('maxImageBytes'), 2), 3))), body('Download_Graph_image_${slot}'), null)`,
      );
    }
    expect(actions.Download_image_5_if_present).toBeUndefined();
    expect(actions.Image_download_results.type).toBe('Compose');
    expect(actions.Image_download_results.inputs).toEqual([1, 2, 3, 4].map(
      (slot) => `@outputs('Capture_downloaded_image_${slot}')`,
    ));
    expect(actions.Image_download_results.runtimeConfiguration.secureData.properties).toEqual(['inputs']);
    expect(JSON.stringify(actions)).not.toContain("@result('Download_graph_images')");
    expect(JSON.stringify(actions)).not.toMatch(/"(Foreach|AppendToArrayVariable|SetVariable)"/);
    expect(template.resources[0].properties.definition.parameters.maxImageBytes.defaultValue).toBe(5 * 1024 * 1024);
    expect(actions.Image_download_results.runAfter).toEqual({
      Download_image_1_if_present: ['Succeeded', 'Failed', 'TimedOut'],
      Download_image_2_if_present: ['Succeeded', 'Failed', 'TimedOut'],
      Download_image_3_if_present: ['Succeeded', 'Failed', 'TimedOut'],
      Download_image_4_if_present: ['Succeeded', 'Failed', 'TimedOut'],
    });
    expect(replyActions.Invoke_bot.actions.Invoke_QA_Bot.inputs.body).toBe("@body('Validate_prepared_activity')");
    expect(replyActions.Invoke_bot.runAfter).toEqual({
      Resolve_image_bytes: ['Succeeded', 'Failed', 'TimedOut'],
    });
    expect(replyActions.Invoke_bot.type).toBe('Scope');
    expect(replyActions.Invoke_bot.actions.Invoke_QA_Bot.runtimeConfiguration.secureData.properties).toContain('inputs');
  });

  it('validates preparation before invoking the bot and does not mask failures', () => {
    const invokeActions = replyActions.Invoke_bot.actions;
    expect(Object.keys(invokeActions).sort()).toEqual(['Invoke_QA_Bot', 'Validate_prepared_activity']);
    expect(invokeActions.Validate_prepared_activity.type).toBe('ParseJson');
    expect(invokeActions.Validate_prepared_activity.inputs).toEqual({
      content: "@body('Prepare_activity_images')",
      schema: {
        type: 'object',
        properties: { type: { type: 'string', enum: ['message'] } },
        required: ['type'],
      },
    });
    expect(invokeActions.Validate_prepared_activity.runtimeConfiguration.secureData.properties).toEqual([
      'inputs',
    ]);
    expect(invokeActions.Invoke_QA_Bot.runAfter).toEqual({
      Validate_prepared_activity: ['Succeeded'],
    });
    expect(invokeActions.Invoke_QA_Bot.inputs.retryPolicy.type).toBe('none');
  });

  it('sends one complete JSON request instead of using the Logic Apps chunked-upload protocol', () => {
    const invoke = replyActions.Invoke_bot.actions.Invoke_QA_Bot;
    expect(invoke.inputs.method).toBe('POST');
    expect(invoke.inputs.headers['Content-Type']).toBe('application/json');
    expect(invoke.runtimeConfiguration).not.toHaveProperty('contentTransfer');
    expect(invoke.runtimeConfiguration.secureData.properties).toEqual(['inputs']);
  });

  it('groups parallel image preparation between conversion and bot invocation', () => {
    expect(Object.keys(replyActions).sort()).toEqual(['Invoke_bot', 'Resolve_image_bytes', 'convertActivity']);
    expect(replyActions.Resolve_image_bytes.type).toBe('Scope');
    expect(replyActions.Resolve_image_bytes.runAfter).toEqual({ convertActivity: ['Succeeded'] });
    expect(actions.Filter_image_attachments.runAfter).toEqual({});
    expect(actions.Prepare_activity_images.runAfter).toEqual({ Image_download_results: ['Succeeded'] });
    expect(actions.Prepare_activity_images.inputs.explicitDependencies.actions).toEqual([
      'convertActivity', 'Image_download_results',
    ]);
    expect(actions.Prepare_activity_images.runtimeConfiguration.secureData.properties).toEqual(['outputs']);
  });

  it('replaces only matching image URLs with connector-supplied base64 bytes', () => {
    const attachments = [
      { contentType: 'text/html', content: '<p>Why did CI fail?</p>' },
      { contentType: 'image/*', contentUrl: graphImage },
      { contentType: 'image/*', contentUrl: 'https://contoso.sharepoint.com/image.png' },
    ];
    const result = prepare(attachments, [downloaded()]);
    expect(result.text).toBe('Why did CI fail?');
    expect(result.attachments).toEqual([
      attachments[0],
      { contentType: 'image/*', contentUrl: dataUri },
      attachments[2],
    ]);
    expect(attachments[1].contentUrl).toBe(graphImage);
  });

  it('handles no images, failed requests, and unused slots without stopping the reply', () => {
    const attachment = { contentType: 'image/*', contentUrl: graphImage };
    expect(prepare([attachment], []).attachments).toEqual([attachment]);
    expect(prepare([attachment], [null, null, null, null]).attachments).toEqual([attachment]);
    expect(prepare([], []).attachments).toEqual([]);
  });

  it('normalizes connector content-type headers', () => {
    const result = prepare(
      [{ contentType: 'image/*', contentUrl: graphImage }],
      [downloaded(graphImage, { '$content-type': 'Image/PNG; charset=binary', '$content': png })],
    );
    expect(result.attachments[0].contentUrl).toBe(dataUri);
  });

  it('uses direct captured bytes without foreach execution-result metadata', () => {
    const second = graphImage.replace('/hostedContents/i/', '/hostedContents/second/');
    const result = prepare(
      [
        { contentType: 'image/*', contentUrl: graphImage },
        { contentType: 'image/*', contentUrl: second },
      ],
      [null, downloaded(second), null, null],
    );
    expect(result.attachments[0].contentUrl).toBe(graphImage);
    expect(result.attachments[1].contentUrl).toBe(dataUri);
  });

  it('forwards all four captured images', () => {
    const urls = [1, 2, 3, 4].map((slot) => graphImage.replace('/hostedContents/i/', `/hostedContents/${slot}/`));
    const result = prepare(
      urls.map((contentUrl) => ({ contentType: 'image/*', contentUrl })),
      urls.map((url) => downloaded(url)),
    );
    expect(result.attachments.map((attachment: Attachment) => attachment.contentUrl)).toEqual([
      dataUri, dataUri, dataUri, dataUri,
    ]);
  });

  it('preserves attachment order when image results arrive in a different order', () => {
    const urls = [1, 2, 3, 4].map((image) => graphImage.replace('/hostedContents/i/', `/hostedContents/${image}/`));
    const images = urls.map((url, index) => downloaded(url, {
      '$content-type': 'image/png',
      '$content': Buffer.from(`image-${index}`).toString('base64'),
    }));
    const result = prepare(
      urls.map((contentUrl) => ({ contentType: 'image/*', contentUrl })),
      images.slice().reverse(),
    );
    expect(result.attachments.map((attachment: Attachment) => attachment.contentUrl)).toEqual(
      images.map((image) => `data:image/png;base64,${image.body!['$content']}`),
    );
  });

  it('keeps downloads from different messages separate', () => {
    const second = graphImage.replace('/messages/m/', '/messages/other/');
    const firstActivity = prepare(
      [{ contentType: 'image/*', contentUrl: graphImage }],
      [downloaded(graphImage)],
    );
    const secondActivity = prepare(
      [{ contentType: 'image/*', contentUrl: second }],
      [downloaded(second, { '$content-type': 'image/jpeg', '$content': '/9j/' })],
    );
    expect(firstActivity.attachments[0].contentUrl).toBe(dataUri);
    expect(secondActivity.attachments[0].contentUrl).toBe('data:image/jpeg;base64,/9j/');
  });

  it('forwards the maximum-size capture and preserves the URL for an oversized capture', () => {
    const attachment = { contentType: 'image/*', contentUrl: graphImage };
    const maxLength = 4 * Math.ceil(5 * 1024 * 1024 / 3);
    const accepted = prepare(
      [attachment],
      [downloaded(graphImage, { '$content-type': 'image/png', '$content': 'A'.repeat(maxLength) })],
    );
    expect(accepted.attachments[0].contentUrl.length).toBe('data:image/png;base64,'.length + maxLength);
    expect(prepare([attachment], [{
      url: graphImage, body: null,
    }]).attachments).toEqual([attachment]);
  });

  it('reports an invalid connector envelope rather than silently dropping the image', () => {
    expect(() => prepare(
      [{ contentType: 'image/*', contentUrl: graphImage }],
      [downloaded(graphImage, {})],
    )).toThrow('Invalid Teams image body');
  });
});
