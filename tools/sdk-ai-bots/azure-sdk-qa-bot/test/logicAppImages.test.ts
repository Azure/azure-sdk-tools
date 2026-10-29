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
  name: string;
  status?: string;
  outputs?: DownloadResult[] | {
    url: string;
    body: Record<string, string> | null;
  };
}

function prepare(attachments: Attachment[], results: DownloadResult[]) {
  const run = new Function('workflowContext', actions.Prepare_activity_images.inputs.code);
  return run({
    actions: {
      convertActivity: { outputs: { body: { text: 'Why did CI fail?', attachments } } },
      Image_download_results: { outputs: { body: results } },
    },
  });
}

function downloaded(
  url = graphImage,
  body: Record<string, string> = { '$content-type': 'image/png', '$content': png },
): DownloadResult {
  return { name: 'Capture_downloaded_image', status: 'Succeeded', outputs: { url, body } };
}

describe('Logic App connector image forwarding', () => {
  it('uses the verified Teams HTTP action headers and existing connection', () => {
    const download = actions.Download_graph_images.actions.Download_Graph_image;
    expect(download.type).toBe('ApiConnection');
    expect(download.inputs.path).toBe('/httprequest');
    expect(download.inputs.method).toBe('post');
    expect(download.inputs.headers.Method).toBe('GET');
    expect(download.inputs.headers.Uri).toBe("@items('Download_graph_images')?['contentUrl']");
    expect(download.inputs.host.connection.name).toBe("@parameters('$connections')['teams']['connectionId']");
    expect(download.inputs.retryPolicy.type).toBe('none');
    expect(download.runtimeConfiguration.secureData.properties).toContain('outputs');
    expect(actions.Prepare_activity_images.inputs.code.length).toBeLessThanOrEqual(1024);
  });

  it('limits downloads to the first four image candidates without shared variables', () => {
    expect(actions.Filter_graph_hosted_images.inputs.from).toBe("@take(body('Filter_image_attachments'), 4)");
    expect(actions.Filter_graph_hosted_images.inputs.where).toContain('https://graph.microsoft.com/');
    expect(actions.Filter_graph_hosted_images.inputs.where).toContain('/hostedContents/');
    expect(actions.Download_graph_images.runtimeConfiguration.concurrency.repetitions).toBe(1);
    expect(actions.Image_download_results.inputs.from).toBe("@result('Download_graph_images')");
    expect(actions.Image_download_results.inputs.where).toBe("@equals(item()?['name'], 'Capture_downloaded_image')");
    const capture = actions.Download_graph_images.actions.Capture_downloaded_image;
    expect(capture.runtimeConfiguration.secureData.properties).toEqual(['inputs']);
    expect(capture.inputs.body).toBe(
      "@if(lessOrEquals(length(coalesce(body('Download_Graph_image')?['$content'], '')), mul(4, div(add(parameters('maxImageBytes'), 2), 3))), body('Download_Graph_image'), null)",
    );
    expect(template.resources[0].properties.definition.parameters.maxImageBytes.defaultValue).toBe(5 * 1024 * 1024);
    expect(actions.Image_download_results.runAfter.Download_graph_images).toEqual(['Succeeded', 'Failed', 'TimedOut']);
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

  it('handles empty downloads, failed requests, and skipped capture actions', () => {
    const attachment = { contentType: 'image/*', contentUrl: graphImage };
    expect(prepare([attachment], []).attachments).toEqual([attachment]);
    expect(prepare([attachment], [
      { name: 'Download_Graph_image', status: 'Failed' },
      { name: 'Capture_downloaded_image', status: 'Skipped' },
    ]).attachments).toEqual([attachment]);
    expect(prepare([], []).attachments).toEqual([]);
  });

  it('normalizes connector content-type headers', () => {
    const result = prepare(
      [{ contentType: 'image/*', contentUrl: graphImage }],
      [downloaded(graphImage, { '$content-type': 'Image/PNG; charset=binary', '$content': png })],
    );
    expect(result.attachments[0].contentUrl).toBe(dataUri);
  });

  it('handles grouped foreach iteration results', () => {
    const result = prepare(
      [{ contentType: 'image/*', contentUrl: graphImage }],
      [{ name: 'Capture_downloaded_image', outputs: [downloaded()] }],
    );
    expect(result.attachments[0].contentUrl).toBe(dataUri);
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
      name: 'Capture_downloaded_image',
      status: 'Succeeded',
      outputs: { url: graphImage, body: null },
    }]).attachments).toEqual([attachment]);
  });

  it('reports an invalid connector envelope rather than silently dropping the image', () => {
    expect(() => prepare(
      [{ contentType: 'image/*', contentUrl: graphImage }],
      [downloaded(graphImage, {})],
    )).toThrow('Invalid Teams image body');
  });
});
