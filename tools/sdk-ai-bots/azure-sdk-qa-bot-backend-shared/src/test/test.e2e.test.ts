import axios from 'axios';
import path from 'path';
import dotenv from 'dotenv';
import { describe, expect, it } from 'vitest';

const __dirname = import.meta.dirname;
const envPath = path.resolve(__dirname, '../../env/.env.local');
dotenv.config({ path: envPath });

describe('E2E Test', () => {
  it('should send a request to the preprocess endpoint', async () => {
    const apiUrl = 'http://localhost:3000/api/prompts/preprocess';
    const apiKey = process.env.API_KEY as string;

    // Call the function to send the request
    const response = await axios.post(
      apiUrl,
      {
        text: 'hello world',
        images: [
          'https://raw.githubusercontent.com/wanlwanl/wanl-fork-azure-sdk-tools/refs/heads/wanl/ocr/tools/sdk-ai-bots/azure-sdk-qa-bot/test/images/ocr-eng.png',
        ],
      },
      {
        headers: {
          'x-api-key': apiKey,
        },
      }
    );

    expect(response.status).to.equal(200);
    expect(JSON.stringify(response.data)).to.equal(
      JSON.stringify({
        text: "\n# Question from user \n\nhello world\n\n## Additional information from images\n\n### Content from image-0: https://raw.githubusercontent.com/wanlwanl/wanl-fork-azure-sdk-tools/refs/heads/wanl/ocr/tools/sdk-ai-bots/azure-sdk-qa-bot/test/images/ocr-eng.png\n\nDebugger listening on ws://127.0.0.1:9239/9d83219c-0d2e-40e9-a990-e9bde4d586a8\nFor help, see: https://nodejs.org/en/docs/inspector\nDebugger attached.\nDebugger attached.\nBot Started, app listening to { address: ' :: '\nfamily: 'IPv6', port: 3978 }\n\n\n\n\n\n\n## Additional information from links\n\n\n\n\n\n",
        warnings: [],
      })
    );
  });
});
