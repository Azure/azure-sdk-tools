import axios from 'axios';
import path from 'path';
import dotenv from 'dotenv';
import { describe, expect, it } from 'vitest';

const __dirname = import.meta.dirname;
const envPath = path.resolve(__dirname, '../../env/.env.local');
dotenv.config({ path: envPath });

describe('Preprocess API Test', () => {
  const apiUrl = 'http://localhost:3000/api/prompts/preprocess';
  const apiKey = process.env.API_KEY as string;

  it('should return warning with unsupported or invalid links', async () => {
    // Call the function to send the request
    const response = await axios.post(
      apiUrl,
      {
        text: 'contains a invalid link: https://invalid.com/invalid',
        images: ['https://invalid.com/invalid.png'],
      },
      {
        headers: {
          'x-api-key': apiKey,
        },
      }
    );
    expect(response.status).to.equal(200);
    expect(response.data.warnings.length).to.equal(2);
  });

  it('should parse inline link to content', async () => {
    // Call the function to send the request
    const response = await axios.post(
      apiUrl,
      {
        text: 'hello world give me the change summary of this PR for tsp: https://github.com/Azure/azure-rest-api-specs/pull/34890',
      },
      {
        headers: {
          'x-api-key': apiKey,
        },
      }
    );
    expect(response.status).to.equal(200);
    expect(JSON.stringify(response.data)).to.contain('Updating MongoDB Atlas client.tsp');
  });

  it('should convert image to content', async () => {
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
