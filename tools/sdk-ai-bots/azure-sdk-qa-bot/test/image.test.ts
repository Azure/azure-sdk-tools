import { describe, expect, it } from 'vitest';
import { ImageTextExtractor } from '../src/input/ImageContentExtractor.js';
import axios from 'axios';

describe('Image OCR', () => {
  it('should recognize text in image', async () => {
    const imageUrl =
      'https://raw.githubusercontent.com/wanlwanl/wanl-fork-azure-sdk-tools/refs/heads/wanl/ocr/tools/sdk-ai-bots/azure-sdk-qa-bot/test/images/ocr-eng.png';
    const imageTextExtractor = new ImageTextExtractor();

    const response = await axios.get(imageUrl, {
      responseType: 'stream',
    });
    const result = await imageTextExtractor.recognizeText(response);
    expect(result.text).toContain('Bot Started');
  });
});
