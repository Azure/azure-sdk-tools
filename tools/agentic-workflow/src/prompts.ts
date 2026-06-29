/**
 * Load + render the durable prompt templates with run variables. Trivial `{{var}}` substitution —
 * no template engine, by design.
 */
import * as fs from "node:fs";
import { fileURLToPath } from "node:url";
import * as path from "node:path";

const here = path.dirname(fileURLToPath(import.meta.url));
// Templates ship next to the package (../prompts relative to dist/ or src/).
const PROMPTS_DIR = path.resolve(here, "..", "prompts");

export type PromptName =
    | "01-research"
    | "02-assumptions"
    | "03-classify"
    | "04-research-item"
    | "05-plan"
    | "06-implement"
    | "critique"
    | "revise";

export function loadTemplate(name: PromptName, dir: string = PROMPTS_DIR): string {
    return fs.readFileSync(path.join(dir, `${name}.md`), "utf8");
}

/** Replace every `{{key}}` with vars[key]; unknown keys render as an empty string. */
export function render(template: string, vars: Record<string, string | undefined>): string {
    return template.replace(/\{\{\s*([a-zA-Z0-9_]+)\s*\}\}/g, (_, key: string) => vars[key] ?? "");
}

export function renderTemplate(name: PromptName, vars: Record<string, string | undefined>, dir?: string): string {
    return render(loadTemplate(name, dir), vars);
}
