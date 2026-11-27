import winston from 'winston';

const SENSITIVE_PATTERNS: Array<[RegExp, string]> = [
  [/\b(?:gh[pousr]_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]{20,})\b/g, '[redacted-secret]'],
  [/\bBearer\s+[A-Za-z0-9\-._~+/]{16,}=*/g, 'Bearer [redacted-secret]'],
  [/[?&]sig=[A-Za-z0-9%+/=]{16,}/gi, '[redacted-secret]'],
  [/https:\/\/teams\.microsoft\.com\/l\/(?:message|channel|chat|meetup-join)\/[^\s)>\]]+/gi, '[redacted-teams-link]'],
  [/\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b/g, '[redacted-email]'],
];

function redactString(value: string): string {
  return SENSITIVE_PATTERNS.reduce((acc, [pat, repl]) => acc.replace(pat, repl), value);
}

function redactValue(value: unknown): unknown {
  if (typeof value === 'string') return redactString(value);
  if (Array.isArray(value)) return value.map(redactValue);
  if (value && typeof value === 'object') {
    const out: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(value)) {
      out[k] = redactValue(v);
    }
    return out;
  }
  return value;
}

// Redact secrets, Teams links, and emails across both log messages and top-level metadata fields.
const sanitizeLogFormat = winston.format((info) => {
  for (const key of Object.keys(info)) {
    // Preserve Winston transport metadata fields.
    if (key === 'level' || key === 'timestamp') continue;
    info[key] = redactValue(info[key]);
  }
  return info;
});

export const logger = winston.createLogger({
  // TODO: add env vars for log level
  level: process.env.LOG_LEVEL || 'info',

  format: winston.format.combine(
    sanitizeLogFormat(),
    winston.format.json(),
    winston.format.timestamp(),
    winston.format.errors({ stack: true }),
    winston.format.splat()
  ),
  defaultMeta: { service: 'azure-sdk-qa-teams-bot' },
  transports: [
    new winston.transports.Console({
      level: 'info',
      stderrLevels: [],
    }),

    new winston.transports.Console({
      level: 'error',
      stderrLevels: ['warn', 'error'],
      consoleWarnLevels: ['warn'],
    }),
  ],
});
