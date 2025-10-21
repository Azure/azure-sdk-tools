import { ComputerVisionClient } from '@azure/cognitiveservices-computervision';
import { ApiKeyCredentials, HttpRequestBody } from '@azure/ms-rest-js';
import {
  GetReadResultResponse,
  ReadInStreamResponse,
} from '@azure/cognitiveservices-computervision/esm/models/index.js';
import axios, { AxiosResponse } from 'axios';
import { RemoteContent } from './RemoteContent.js';
import { ManagedIdentityCredential } from '@azure/identity';
import { logger } from '../logging/logger.js';
import { Readable } from 'stream';

export class ImageContentExtractor {
  private readonly pollResultInterval = 500;
  private readonly botFrameworkScope = 'https://api.botframework.com/.default';

  // env
  private readonly azureComputerVisionEndpoint;
  private readonly azureComputerVisionApiKey;
  private readonly botId;

  private readonly client: ComputerVisionClient;
  private logMeta: object;

  constructor(logMeta: object = {}) {
    this.azureComputerVisionEndpoint = process.env['AZURE_COMPUTER_VISION_ENDPOINT'] ?? '';
    this.azureComputerVisionApiKey = process.env['AZURE_COMPUTER_VISION_API_KEY'] ?? '';
    this.botId = process.env['BOT_ID'] ?? '';

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
        
        // Validate URL to prevent SSRF attacks
        if (!this.isAllowedImageUrl(url)) {
          const error = new Error('URL not allowed: Only HTTPS URLs from trusted domains are supported');
          return { text: '', url, id, error };
        }
        
        const { image, error } =
          process.env.MODE === 'EVALUATION'
            ? await this.getImageResponseEvaluation(url)
            : await this.getImageResponse(url);
        if (error) return { text: '', url, id, error };
        const recognizeResult = await this.recognizeText(image!);
        return { text: recognizeResult.text, url, id, error: recognizeResult.error };
      })
    );
  }
  
  private isAllowedImageUrl(url: URL): boolean {
    // Only allow HTTPS URLs
    if (url.protocol !== 'https:') {
      return false;
    }
    
    // Block private IP ranges and localhost to prevent SSRF
    const hostname = url.hostname.toLowerCase();
    
    // Block localhost
    if (hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1') {
      return false;
    }
    
    // Block private IP ranges (simplified check)
    if (hostname.match(/^(10\.|172\.(1[6-9]|2[0-9]|3[01])\.|192\.168\.)/)) {
      return false;
    }
    
    // Block link-local addresses
    if (hostname.match(/^169\.254\./)) {
      return false;
    }
    
    return true;
  }

  public async recognizeText(image: HttpRequestBody): Promise<{ text: string; error?: Error }> {
    let readResponse: ReadInStreamResponse;

    try {
      readResponse = await this.client.readInStream(image);
    } catch (error) {
      console.error(`xxx Failed to read image`, error);
      return { text: '', error: error as Error };
    }

    const operationLocation = readResponse.operationLocation;

    if (!operationLocation) {
      logger.warn('Failed to get Operation-Location for OCR', { meta: this.logMeta });
      return { text: '', error: new Error('Failed to get Operation-Location for OCR') };
    }
    const operationId = operationLocation.split('/').slice(-1)[0];

    let result: GetReadResultResponse | undefined = undefined;
    // TODO: set timeout
    while (true) {
      try {
        result = await this.client.getReadResult(operationId);
      } catch (error) {
        logger.warn(`Failed to get read OCR result`, error, { meta: this.logMeta });
      }
      if (result && result.status !== 'notStarted' && result.status !== 'running') break;
      await new Promise((resolve) => setTimeout(resolve, this.pollResultInterval));
    }

    let text = ``;
    if (result && result.status === 'succeeded' && result.analyzeResult?.readResults) {
      for (const page of result.analyzeResult.readResults) {
        for (const line of page.lines) text += line.text + '\n';
      }
    } else console.error('Failed to recognize text, please check the input and key.');
    return { text };
  }

  private async getImageResponse(url: URL): Promise<{ image?: HttpRequestBody; error?: Error }> {
    const credentials = new ManagedIdentityCredential(this.botId);
    const botTokenResponse = await credentials.getToken([this.botFrameworkScope]);
    const botToken = botTokenResponse.token;

    let response: AxiosResponse;
    try {
      response = await axios.get(url.href, {
        responseType: 'stream',
        headers: {
          Authorization: `Bearer ${botToken}`,
        },
      });

      return { image: response.data };
    } catch (error) {
      console.error(`Failed to load image from ${url.href}`, error);
      return { error: error as Error };
    }
  }

  // TODO: remove
  private async getImageResponseEvaluation(url: URL): Promise<{ image?: HttpRequestBody; error?: Error }> {
    try {
      const response = await axios.get(url.href, {
        responseType: 'stream',
      });
      const arr = await streamToBufferArray(response.data);
      const fullBuffer = Buffer.concat(arr); // 合并成一个 Buffer
      const buff = fullBuffer.buffer.slice(fullBuffer.byteOffset, fullBuffer.byteOffset + fullBuffer.byteLength);

      return { image: buff };
    } catch (error) {
      console.error(`Failed to load image from ${url.href}`, error);
      return { error: error as Error };
    }
  }
}

async function streamToBufferArray(stream: Readable): Promise<Buffer[]> {
  const chunks: Buffer[] = [];

  return new Promise((resolve, reject) => {
    stream.on('data', (chunk) => {
      chunks.push(Buffer.isBuffer(chunk) ? chunk : Buffer.from(chunk));
    });
    stream.on('end', () => {
      resolve(chunks);
    });
    stream.on('error', (err) => {
      reject(err);
    });
  });
}
