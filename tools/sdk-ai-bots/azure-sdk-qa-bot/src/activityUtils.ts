import { Activity, TurnContext } from 'botbuilder';
import { logger } from './logging/logger.js';

/**
 * Send Teams activity with retry
 * 
 * @param turnContext The context for a turn of a bot
 * @param activityOrText The activity or text to send.
 * @param maxRetries The max retry times.
 * @returns A promise with a ResourceResponse.
 */
export async function sendActivityWithRetry(
  turnContext: TurnContext,
  activityOrText: string | Partial<Activity>,
  maxRetries: number = 5
) {
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      return await turnContext.sendActivity(activityOrText);
    } catch (e: any) {
      const status = e?.statusCode ?? e?.response?.status ?? e?.status;
      if (status !== 429) throw e;

      logger.info("429 exception occurred. retry to send activity.");
      await retryHelper(e, attempt, maxRetries);
    }
  }
}

/**
 * Update Teams activity with retry
 * 
 * @param turnContext The context for a turn of a bot
 * @param activity The replacement for the original activity.
 * @param maxRetries The max retry times.
 * @returns A promise with a ResourceResponse.
 */
export async function updateActivityWithRetry(
  turnContext: TurnContext,
  activity: Partial<Activity>,
  maxRetries: number = 5
) {
  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      return await turnContext.updateActivity(activity);
    } catch (e: any) {
      const status = e?.statusCode ?? e?.response?.status ?? e?.status;
      if (status !== 429) throw e;

      logger.info("429 exception occurred. retry to update activity.");
      await retryHelper(e, attempt, maxRetries);
    }
  }
}

async function retryHelper(e: any, attempt: number, maxRetries: number) {
    if (attempt === maxRetries) {
        logger.warn("Retry attempts have exceeded the configured retry limit.")
        throw e;
      }
    // Retry-After might be in seconds; fall back to exponential if missing
    let retryAfter =
        e?.response?.headers?.["retry-after"] ??
        e?.response?.headers?.["Retry-After"] ??
        (typeof e?.response?.headers?.get === "function" ? e?.response?.headers.get("retry-after") : undefined);
    if (!retryAfter && e?.headers) {
        retryAfter =
        e?.headers?.["retry-after"] ??
        e?.headers?.["Retry-After"];
    }

    const retryAfterMS = retryAfterToMs(retryAfter);
    const baseDelayMs = retryAfterMS
    ? retryAfterMS
    : Math.min(60000, 1000 * Math.pow(2, attempt));

    // add jitter to avoid synchronized retries across instances
    const jitterMs = Math.floor(Math.random() * 250);
    await new Promise((r) => setTimeout(r, baseDelayMs + jitterMs));
}


function retryAfterToMs(value: string | null | undefined): number | undefined {
  if (!value) return undefined;

  // 1) seconds form
  const seconds = Number(value);
  if (Number.isFinite(seconds)) return Math.max(0, seconds) * 1000;

  // 2) HTTP date form
  const dateMs = Date.parse(value);
  if (!Number.isNaN(dateMs)) return Math.max(0, dateMs - Date.now());

  return undefined;
}
