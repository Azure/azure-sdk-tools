import { ComputerVisionClient } from '@azure/cognitiveservices-computervision';
import { ApiKeyCredentials } from '@azure/ms-rest-js';
import {
  GetReadResultResponse,
  ReadInStreamResponse,
} from '@azure/cognitiveservices-computervision/esm/models/index.js';
import axios, { AxiosResponse } from 'axios';
import { RemoteContent } from './RemoteContent.js';
import { ManagedIdentityCredential } from '@azure/identity';
import { logger } from '../logging/logger.js';

export class ImageTextExtractor {
  private readonly pollResultInterval = 500;
  private readonly botFrameworkScope = 'https://api.botframework.com/.default';

  // env
  private readonly azureComputerVisionEndpoint = process.env['AZURE_COMPUTER_VISION_ENDPOINT'];
  private readonly azureComputerVisionApiKey = process.env['AZURE_COMPUTER_VISION_API_KEY'];
  private readonly botId = process.env['BOT_ID'];

  private readonly credentials = new ManagedIdentityCredential(this.botId);
  private readonly client: ComputerVisionClient;
  private logMeta?: object;

  constructor(logMeta?: object) {
    this.logMeta = logMeta;
    const creds = new ApiKeyCredentials({
      inHeader: {
        'Ocp-Apim-Subscription-Key': this.azureComputerVisionApiKey,
      },
    });
    this.client = new ComputerVisionClient(creds, this.azureComputerVisionEndpoint);
  }

  // TODO: add retry
  public async extract(imageUrls: URL[]): Promise<RemoteContent[]> {
    return Promise.all(
      imageUrls.map(async (url, index) => {
        const id = `image-${index}`;

        const botTokenResponse = await this.credentials.getToken([this.botFrameworkScope]);
        const botToken = botTokenResponse.token;

        let response: AxiosResponse;
        try {
          response = await axios.get(url.href, {
            responseType: 'stream',
            headers: {
              Authorization: `Bearer ${botToken}`,
            },
          });
        } catch (error) {
          console.error(`Failed to load image from ${url.href}`, error);
          return { text: '', url, id, error: error };
        }

        const recognizeResult = await this.recognizeText(response);
        return { text: recognizeResult.text, url, id, error: recognizeResult.error };
      })
    );
  }

  public async recognizeText(stream: AxiosResponse): Promise<{ text: string; error?: Error }> {
    let readResponse: ReadInStreamResponse;

    try {
      readResponse = await this.client.readInStream(() => stream.data);
    } catch (error) {
      console.error(`Failed to read image`, error);
      return { text: '', error };
    }

    const operationLocation = readResponse.operationLocation;

    if (!operationLocation) {
      logger.warn('Failed to get Operation-Location for OCR', this.logMeta);
      return { text: '', error: new Error('Failed to get Operation-Location for OCR') };
    }
    const operationId = operationLocation.split('/').slice(-1)[0];

    let result: GetReadResultResponse;
    // TODO: set timeout
    while (true) {
      try {
        result = await this.client.getReadResult(operationId);
      } catch (error) {
        logger.warn(`Failed to get read OCR result`, error, this.logMeta);
      }
      if (result.status !== 'notStarted' && result.status !== 'running') break;
      await new Promise((resolve) => setTimeout(resolve, this.pollResultInterval));
    }

    let text = ``;
    if (result.status === 'succeeded' && result.analyzeResult?.readResults) {
      for (const page of result.analyzeResult.readResults) {
        for (const line of page.lines) text += line.text + '\n';
      }
    } else console.error('Failed to recognize text, please check the input and key.');
    return { text };
  }
}
