import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

const template = JSON.parse(readFileSync(
  new URL('../../azure-sdk-qa-bot-agent/pipelines/logicapp/template.json', import.meta.url),
  'utf8',
));
const actions = template.resources[0].properties.definition.actions.For_each.actions
  .Thread_Message_From_User.actions.Need_Reply.actions.Should_Reply.actions;

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
      convertActivity: { outputs: { body: { text: 'Why did CI fail?', attachments } } },
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

describe('Logic App connector image forwarding', () => {
  it('uses the verified Teams HTTP action headers and existing connection', () => {
    for (let slot = 1; slot <= 4; slot++) {
      const download = actions[`Download_graph_image_slot_${slot}`].actions[`Download_Graph_image_${slot}`];
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
      const condition = actions[`Download_graph_image_slot_${slot}`];
      expect(condition.type).toBe('If');
      expect(condition.expression).toBe(`@greater(length(body('Filter_graph_hosted_images')), ${slot - 1})`);
      expect(condition.runAfter).toEqual(slot === 1
        ? { Filter_graph_hosted_images: ['Succeeded'] }
        : { [`Download_graph_image_slot_${slot - 1}`]: ['Succeeded', 'Failed', 'TimedOut'] });
      const capture = condition.actions[`Capture_downloaded_image_${slot}`];
      expect(capture.runAfter).toEqual({ [`Download_Graph_image_${slot}`]: ['Succeeded'] });
      expect(capture.runtimeConfiguration.secureData.properties).toEqual(['inputs']);
      expect(capture.inputs.url).toBe(`@body('Filter_graph_hosted_images')[${slot - 1}]['contentUrl']`);
      expect(capture.inputs.body).toBe(
        `@if(lessOrEquals(length(coalesce(body('Download_Graph_image_${slot}')?['$content'], '')), mul(4, div(add(parameters('maxImageBytes'), 2), 3))), body('Download_Graph_image_${slot}'), null)`,
      );
    }
    expect(actions.Download_graph_image_slot_5).toBeUndefined();
    expect(actions.Image_download_results.type).toBe('Compose');
    expect(actions.Image_download_results.inputs).toEqual([1, 2, 3, 4].map(
      (slot) => `@outputs('Capture_downloaded_image_${slot}')`,
    ));
    expect(actions.Image_download_results.runtimeConfiguration.secureData.properties).toEqual(['inputs']);
    expect(JSON.stringify(actions)).not.toContain("@result('Download_graph_images')");
    expect(JSON.stringify(actions)).not.toMatch(/"(Foreach|AppendToArrayVariable|SetVariable)"/);
    expect(template.resources[0].properties.definition.parameters.maxImageBytes.defaultValue).toBe(5 * 1024 * 1024);
    expect(actions.Image_download_results.runAfter).toEqual({
      Download_graph_image_slot_4: ['Succeeded', 'Failed', 'TimedOut'],
    });
    expect(actions.Scope.actions.Invoke_QA_Bot.inputs.body).toBe("@outputs('Prepare_activity_images')");
    expect(actions.Scope.runAfter).toEqual({ Prepare_activity_images: ['Succeeded'] });
    expect(actions.Scope.actions.Invoke_QA_Bot.runtimeConfiguration.secureData.properties).toContain('inputs');
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
