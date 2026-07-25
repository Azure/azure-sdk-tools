/**
 * config.ts — loads and validates config.json (CCR identity, automation
 * accounts, excluded paths, judge settings). Configurable, not hard-coded, so a
 * CCR login change can't silently poison ccrReviewed/ccrSawCode/ccrCoverage.
 */
import * as fs from "node:fs";
import * as path from "node:path";
import { fileURLToPath } from "node:url";

import { z } from "zod";

export const ConfigSchema = z.object({
    ccrLogins: z.array(z.string()).min(1),
    automationLogins: z.array(z.string()),
    excludedPaths: z.array(z.string()),
    minPrs: z.number().int().positive().default(50),
    windowLagDays: z.number().int().nonnegative().default(14),
    ccrEnabledSince: z
        .string()
        .refine((s) => !Number.isNaN(Date.parse(s)), {
            message: "ccrEnabledSince must be a valid date string (or null)",
        })
        .nullable()
        .default(null),
});

export type Config = z.infer<typeof ConfigSchema>;

function defaultConfigPath(): string {
    const here = path.dirname(fileURLToPath(import.meta.url));
    return path.join(here, "..", "config.json");
}

export function loadConfig(configPath?: string): Config {
    const file = configPath ?? defaultConfigPath();
    const raw = fs.readFileSync(file, "utf8");
    const parsed: unknown = JSON.parse(raw);
    return ConfigSchema.parse(parsed);
}

/**
 * Translate a small glob subset (`**`, `*`) to a RegExp for excludedPaths
 * matching. Only the patterns we ship are supported; this is not a full glob.
 */
export function globToRegExp(glob: string): RegExp {
    let re = "";
    for (let i = 0; i < glob.length; i++) {
        const c = glob[i];
        if (c === "*") {
            if (glob[i + 1] === "*") {
                re += ".*";
                i++;
                if (glob[i + 1] === "/") i++;
            } else {
                re += "[^/]*";
            }
        } else if (c === "?") {
            re += "[^/]";
        } else if (c && ".+^${}()|[]\\".includes(c)) {
            re += `\\${c}`;
        } else {
            re += c ?? "";
        }
    }
    return new RegExp(`^${re}$`);
}

export function isExcludedPath(
    filePath: string | null,
    excludedPaths: string[],
): boolean {
    if (!filePath) return false;
    return excludedPaths.some((g) => globToRegExp(g).test(filePath));
}
