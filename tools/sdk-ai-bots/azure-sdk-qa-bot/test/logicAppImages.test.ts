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
      withImage ? [downloaded()] : [],
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
    const download = actions.Download_graph_images.actions.Download_Graph_image;
    expect(download.type).toBe('ApiConnection');
    expect(download.inputs.path).toBe('/httprequest');
    expect(download.inputs.method).toBe('post');
    expect(download.inputs.headers.Method).toBe('GET');
    expect(download.inputs.headers.Uri).toBe("@items('Download_graph_images')['contentUrl']");
    expect(download.inputs.host.connection.name).toBe("@parameters('$connections')['teams']['connectionId']");
    expect(download.inputs.retryPolicy.type).toBe('none');
    expect(download.runtimeConfiguration.secureData.properties).toEqual(['outputs']);
    expect(actions.Prepare_activity_images.inputs.code.length).toBeLessThanOrEqual(1024);
  });

  it('collects the first four image candidates in one sequential loop', () => {
    expect(actions.Filter_graph_hosted_images.inputs.from).toBe("@take(body('Filter_image_attachments'), 4)");
    expect(actions.Filter_graph_hosted_images.inputs.where).toContain('https://graph.microsoft.com/');
    expect(actions.Filter_graph_hosted_images.inputs.where).toContain('/hostedContents/');
    const loop = actions.Download_graph_images;
    expect(loop.type).toBe('Foreach');
    expect(loop.foreach).toBe("@body('Filter_graph_hosted_images')");
    expect(loop.operationOptions).toBe('Sequential');
    expect(loop.runAfter).toEqual({ Filter_graph_hosted_images: ['Succeeded'] });
    expect(Object.keys(loop.actions).sort()).toEqual(['Append_downloaded_image', 'Download_Graph_image']);
    const append = loop.actions.Append_downloaded_image;
    expect(append.type).toBe('AppendToArrayVariable');
    expect(append.inputs.name).toBe('downloadedImages');
    expect(append.inputs.value.url).toBe("@items('Download_graph_images')['contentUrl']");
    expect(append.inputs.value.body).toBe(
      "@if(lessOrEquals(length(coalesce(body('Download_Graph_image')?['$content'], '')), mul(4, div(add(parameters('maxImageBytes'), 2), 3))), body('Download_Graph_image'), null)",
    );
    expect(append.runAfter).toEqual({ Download_Graph_image: ['Succeeded'] });
    // The append directly references secured output; variable actions cannot set secureData.
    expect(append.runtimeConfiguration).toBeUndefined();
    expect(JSON.stringify(actions)).not.toContain('Download_image_1_if_present');
    expect(actions.Image_download_results.type).toBe('Compose');
    expect(actions.Image_download_results.inputs).toBe("@variables('downloadedImages')");
    expect(actions.Image_download_results.runtimeConfiguration.secureData.properties).toEqual(['inputs']);
    expect(JSON.stringify(actions)).not.toContain("@result('Download_graph_images')");
    expect(template.resources[0].properties.definition.parameters.maxImageBytes.defaultValue).toBe(5 * 1024 * 1024);
    expect(actions.Image_download_results.runAfter).toEqual({
      Download_graph_images: ['Succeeded', 'Failed', 'TimedOut'],
    });
    expect(replyActions.Invoke_bot.actions.Invoke_QA_Bot.inputs.body).toBe("@body('Validate_prepared_activity')");
    expect(replyActions.Invoke_bot.runAfter).toEqual({
      Resolve_image_bytes: ['Succeeded', 'Failed', 'TimedOut'],
    });
    expect(replyActions.Invoke_bot.type).toBe('Scope');
    expect(replyActions.Invoke_bot.actions.Invoke_QA_Bot.runtimeConfiguration.secureData.properties).toContain('inputs');
  });

  it('initializes per run and resets per message with no concurrent access to the array', () => {
    const workflowActions = template.resources[0].properties.definition.actions;
    expect(workflowActions.Initialize_downloaded_images).toEqual({
      type: 'InitializeVariable',
      inputs: { variables: [{ name: 'downloadedImages', type: 'array', value: [] }] },
      runAfter: {},
    });
    expect(workflowActions.For_each.operationOptions).toBe('Sequential');
    expect(workflowActions.For_each.runAfter).toEqual({ Initialize_downloaded_images: ['Succeeded'] });
    expect(actions.Reset_downloaded_images).toEqual({
      type: 'SetVariable',
      inputs: { name: 'downloadedImages', value: [] },
      runAfter: {},
    });
    expect(actions.Filter_image_attachments.runAfter).toEqual({ Reset_downloaded_images: ['Succeeded'] });
    expect(JSON.stringify(workflowActions).match(/@variables\('downloadedImages'\)/g)).toHaveLength(1);
    expect(actions.Image_download_results.inputs).toBe("@variables('downloadedImages')");
    expect(actions.Image_download_results.runtimeConfiguration.secureData.properties).toEqual(['inputs']);
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

  it('groups sequential image preparation between conversion and bot invocation', () => {
    expect(Object.keys(replyActions).sort()).toEqual(['Invoke_bot', 'Resolve_image_bytes', 'convertActivity']);
    expect(replyActions.Resolve_image_bytes.type).toBe('Scope');
    expect(replyActions.Resolve_image_bytes.runAfter).toEqual({ convertActivity: ['Succeeded'] });
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

  it('handles no images and failed requests without stopping the reply', () => {
    const attachment = { contentType: 'image/*', contentUrl: graphImage };
    expect(prepare([attachment], []).attachments).toEqual([attachment]);
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
      [downloaded(second)],
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

  it('resets the shared array between batched image, failed-download, and text-only messages', () => {
    const initialize = template.resources[0].properties.definition.actions.Initialize_downloaded_images.inputs.variables[0];
    const reset = actions.Reset_downloaded_images.inputs;
    const append = actions.Download_graph_images.actions.Append_downloaded_image.inputs;
    const variables: Record<string, DownloadResult[]> = { [initialize.name]: structuredClone(initialize.value) };
    const second = graphImage.replace('/messages/m/', '/messages/other/');
    const batch: { attachments: Attachment[]; downloads: DownloadResult[]; expected: Attachment[] }[] = [
      {
        attachments: [{ contentType: 'image/*', contentUrl: graphImage }],
        downloads: [downloaded()],
        expected: [{ contentType: 'image/*', contentUrl: dataUri }],
      },
      {
        attachments: [{ contentType: 'image/*', contentUrl: graphImage }],
        downloads: [],
        expected: [{ contentType: 'image/*', contentUrl: graphImage }],
      },
      { attachments: [], downloads: [], expected: [] },
      {
        attachments: [{ contentType: 'image/*', contentUrl: second }],
        downloads: [downloaded(second, { '$content-type': 'image/jpeg', '$content': '/9j/' })],
        expected: [{ contentType: 'image/*', contentUrl: 'data:image/jpeg;base64,/9j/' }],
      },
    ];
    for (const message of batch) {
      variables[reset.name] = structuredClone(reset.value);
      for (const result of message.downloads) {
        variables[append.name].push(result);
      }
      expect(prepare(message.attachments, variables[initialize.name]).attachments).toEqual(message.expected);
      expect(variables[initialize.name]).toEqual(message.downloads);
    }
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
