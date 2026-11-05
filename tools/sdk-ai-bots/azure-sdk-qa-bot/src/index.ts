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

// Health check endpoint
expressApp.get('/health', (req, res) => {
  try {
    // Perform basic health checks
    const healthStatus = {
      status: 'healthy',
      timestamp: new Date().toISOString(),
      uptime: process.uptime(),
      version: '1.0.0',
    };

    logger.info('Health check requested', {
      ip: req.ip,
      userAgent: req.get('User-Agent'),
    });

    res.status(200).json(healthStatus);
  } catch (error) {
    logger.error('Health check failed', { error: error.message });
    res.status(503).json({
      status: 'unhealthy',
      timestamp: new Date().toISOString(),
      error: 'Internal server error',
    });
  }
});

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
  try {
    // Route received a request to adapter for processing
    await adapter.process(req, res as any, async (context) => {
      // Dispatch to application for routing
      await app.run(context);
    });
  } catch (error) {
    // Bot Framework SDK auth throws BEFORE onTurnError runs (no TurnContext yet).
    // The most common transient failure is `AuthenticationError: Signing Key
    // could not be retrieved` — the connector's on-demand fetch of
    // https://login.botframework.com/v1/.well-known/keys hit a network blip
    // (ERR_STREAM_PREMATURE_CLOSE, connection reset, DNS glitch, ...). It
    // gets logged as an uncaught rejection to stderr and the client sees a
    // hung/failed request. Convert to a clean 401 so Bot Framework Service's
    // built-in delivery retries can succeed on the next attempt (Teams retries
    // channel activities up to 3× with backoff).
    const name = (error as { name?: string })?.name ?? '';
    const message = (error as Error)?.message ?? String(error);
    const statusCode = (error as { statusCode?: number })?.statusCode;
    const isAuthError =
      name === 'AuthenticationError' ||
      /signing key|openid|metadata|jwks|jwt/i.test(message);
    if (isAuthError) {
      logger.warn(`Bot Framework auth failure (transient — Bot Service will retry): ${message}`, {
        statusCode,
      });
      if (!res.headersSent) {
        res.status(statusCode ?? 401).end();
      }
      return;
    }
    // Non-auth error: let onTurnError have logged it if it fired; also log
    // here in case the throw happened before a TurnContext was created.
    logger.error(`Unhandled /api/messages error: ${message}`, {
      name,
      statusCode,
      stack: (error as Error)?.stack,
    });
    if (!res.headersSent) {
      res.status(500).end();
    }
  }
});
