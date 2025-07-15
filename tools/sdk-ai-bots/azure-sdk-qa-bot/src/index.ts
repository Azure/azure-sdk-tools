// Import required packages
import express from 'express';

// This bot's adapter
import adapter from './adapter.js';

// This bot's main dialog.
import app from './app/app.js';
import { logger } from './logging/logger.js';

// Create express application.
const expressApp = express();
expressApp.use(express.json());

const server = expressApp.listen(process.env.port || process.env.PORT || 3978, () => {
  const address = server.address();
  if (!address) {
    logger.error('Server address is not defined');
    return;
  }
  const port = typeof address === 'string' ? address : address.port;
  logger.info(`\nBot Started, ${expressApp.name} listening to ${port}`);
});

// Listen for incoming requests.
expressApp.post('/api/messages', async (req, res) => {
  // Route received a request to adapter for processing
  await adapter.process(req, res as any, async (context) => {
    // Dispatch to application for routing
    await app.run(context);
  });
});
