
import { Activity, TurnContext } from 'botbuilder';
import { logger } from './logging/logger.js';

export async function sendActivityWithRetry(
  turnContext: TurnContext,
  activityOrText: string | Partial<Activity>,
  maxRetries = 5
) {
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      return await turnContext.sendActivity(activityOrText);
    } catch (e: any) {
      const status = e?.statusCode ?? e?.response?.status ?? e?.status;
      if (status !== 429 || attempt === maxRetries) throw e;

      logger.info("429 exception occur. retry to send activity.");
      // Retry-After might be in seconds; fall back to exponential if missing
      const retryAfter =
        Number(e?.response?.headers?.["retry-after"] ?? e?.headers?.["retry-after"]);
      const baseDelayMs = Number.isFinite(retryAfter)
        ? retryAfter * 1000
        : Math.min(60000, 1000 * Math.pow(2, attempt));

      // add jitter to avoid synchronized retries across instances
      const jitterMs = Math.floor(Math.random() * 250);
      await new Promise((r) => setTimeout(r, baseDelayMs + jitterMs));
    }
  }
}


export async function updateActivityWithRetry(
  turnContext: TurnContext,
  activity: Partial<Activity>,
  maxRetries = 5
) {
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      return await turnContext.updateActivity(activity);
    } catch (e: any) {
      const status = e?.statusCode ?? e?.response?.status ?? e?.status;
      if (status !== 429 || attempt === maxRetries) throw e;

      logger.info("429 exception occur. retry to update activity.");
      // Retry-After might be in seconds; fall back to exponential if missing
      const retryAfter =
        Number(e?.response?.headers?.["retry-after"] ?? e?.headers?.["retry-after"]);
      const baseDelayMs = Number.isFinite(retryAfter)
        ? retryAfter * 1000
        : Math.min(60000, 1000 * Math.pow(2, attempt));

      // add jitter to avoid synchronized retries across instances
      const jitterMs = Math.floor(Math.random() * 250);
      await new Promise((r) => setTimeout(r, baseDelayMs + jitterMs));
    }
  }
}