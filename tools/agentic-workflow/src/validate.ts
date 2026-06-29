/**
 * A handful of plain validation functions (thinnerplan T1.2). No schema library: parse, check the
 * few required fields, return readable errors. Only `subitems.json` and the plan block get
 * validation until a real format breaks.
 */
import { parsePlanStages } from "./gates.js";
import type { SubItems, ValidationResult } from "./types.js";

const CLASSIFICATIONS = ["feature", "bug", "refactor", "mixed"];
const ITEM_TYPES = ["feature", "bug", "refactor"];
const OVERLAP = ["low", "medium", "high"];
const ID_RE = /^[a-z0-9]+(-[a-z0-9]+)*$/;

/** Validate the parsed object shape of `subitems.json`. */
export function validateSubitems(input: unknown): ValidationResult {
    const errors: string[] = [];
    const obj = input as Partial<SubItems> | null;

    if (!obj || typeof obj !== "object") {
        return { ok: false, errors: ["subitems.json: not an object"] };
    }
    if (typeof obj.task !== "string" || !obj.task.trim()) {
        errors.push("subitems.json: `task` must be a non-empty string");
    }
    if (!obj.classification || !CLASSIFICATIONS.includes(obj.classification)) {
        errors.push(`subitems.json: \`classification\` must be one of ${CLASSIFICATIONS.join(", ")}`);
    }
    if (!Array.isArray(obj.items) || obj.items.length === 0) {
        errors.push("subitems.json: `items` must be a non-empty array");
        return { ok: errors.length === 0, errors };
    }

    const seen = new Set<string>();
    const ids = new Set(obj.items.map((it) => it?.id).filter((x): x is string => typeof x === "string"));
    obj.items.forEach((it, i) => {
        const where = `items[${i}]`;
        if (!it || typeof it !== "object") {
            errors.push(`${where}: not an object`);
            return;
        }
        if (typeof it.id !== "string" || !ID_RE.test(it.id)) {
            errors.push(`${where}: \`id\` must be kebab-case`);
        } else if (seen.has(it.id)) {
            errors.push(`${where}: duplicate id "${it.id}"`);
        } else {
            seen.add(it.id);
        }
        if (!it.type || !ITEM_TYPES.includes(it.type)) {
            errors.push(`${where}: \`type\` must be one of ${ITEM_TYPES.join(", ")}`);
        }
        if (typeof it.title !== "string" || !it.title.trim()) {
            errors.push(`${where}: \`title\` must be a non-empty string`);
        }
        if (!Array.isArray(it.dependsOn)) {
            errors.push(`${where}: \`dependsOn\` must be an array`);
        } else {
            for (const dep of it.dependsOn) {
                if (typeof dep !== "string" || !ids.has(dep)) {
                    errors.push(`${where}: dependsOn references unknown item "${dep}"`);
                }
            }
        }
        if (!it.overlapRisk || !OVERLAP.includes(it.overlapRisk)) {
            errors.push(`${where}: \`overlapRisk\` must be one of ${OVERLAP.join(", ")}`);
        }
    });

    return { ok: errors.length === 0, errors };
}

/** Parse + validate `subitems.json` text. */
export function validateSubitemsJson(text: string): ValidationResult {
    let parsed: unknown;
    try {
        parsed = JSON.parse(text);
    } catch (e) {
        return { ok: false, errors: [`subitems.json: invalid JSON — ${(e as Error).message}`] };
    }
    return validateSubitems(parsed);
}

const REQUIRED_PLAN_SECTIONS = ["Decisions and rationale", "Step-by-step implementation plan", "Definition of done"];

/** Validate a generated `plan.md`: required prose sections + a parseable gate block. */
export function validatePlan(planMarkdown: string): ValidationResult {
    const errors: string[] = [];
    for (const section of REQUIRED_PLAN_SECTIONS) {
        if (!planMarkdown.toLowerCase().includes(section.toLowerCase())) {
            errors.push(`plan.md: missing required section "${section}"`);
        }
    }
    try {
        parsePlanStages(planMarkdown);
    } catch (e) {
        errors.push((e as Error).message);
    }
    return { ok: errors.length === 0, errors };
}
