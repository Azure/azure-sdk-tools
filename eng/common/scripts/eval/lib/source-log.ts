import { lstat, readFile } from "node:fs/promises";
import { join } from "node:path";
import { SOURCE_LOG_LINES_FILE } from "./grader-log-lines.ts";

const TIMELINE_ID = /^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/i;

export function validateSourceLog(value) {
    if (!value || typeof value !== "object" || Array.isArray(value) || typeof value.jobId !== "string" ||
        typeof value.taskId !== "string" || !TIMELINE_ID.test(value.jobId) || !TIMELINE_ID.test(value.taskId)) {
        throw new Error("Invalid evaluation task identity.");
    }
    const result = { jobId: value.jobId, taskId: value.taskId };
    if (value.line !== undefined) {
        if (!Number.isSafeInteger(value.line) || value.line < 1) throw new Error("Invalid evaluation task line.");
        result.line = value.line;
    }
    if (value.graderLines !== undefined) {
        if (!value.graderLines || typeof value.graderLines !== "object" || Array.isArray(value.graderLines) ||
            !Object.values(value.graderLines).every(line => Number.isSafeInteger(line) && line > 0)) {
            throw new Error("Invalid evaluation grader lines.");
        }
        result.graderLines = value.graderLines;
    }
    return result;
}

export async function selectedLogLines(directory, selected) {
    if (selected.logLines !== SOURCE_LOG_LINES_FILE || !selected.sourceLog) return null;
    try {
        const file = join(directory, SOURCE_LOG_LINES_FILE);
        const metadata = await lstat(file);
        if (!metadata.isFile() || metadata.size < 1 || metadata.size > 8 * 1024 * 1024) return null;
        const bytes = await readFile(file);
        if (bytes.length !== metadata.size) return null;
        const value = JSON.parse(bytes.toString("utf8"));
        if (value.schemaVersion !== 1 || value.jobId !== selected.sourceLog.jobId || value.taskId !== selected.sourceLog.taskId ||
            !value.trials || typeof value.trials !== "object" || Array.isArray(value.trials)) return null;
        return value.trials;
    } catch { return null; }
}

export function sourceLogForTrial(trial, selectedSourceLog, locations) {
    let result = validateSourceLog(selectedSourceLog);
    if (locations && Object.hasOwn(locations, trial.itemId)) {
        try {
            const location = locations[trial.itemId];
            const checked = validateSourceLog({ ...result, line: location.line, graderLines: location.graderLines });
            const names = new Set((trial.gradeResult?.details ?? trial.grade?.details ?? []).map(grade => grade.name));
            if (checked.graderLines) checked.graderLines = Object.fromEntries(Object.entries(checked.graderLines).filter(([name]) => names.has(name)));
            result = checked;
        } catch {}
    }
    return result;
}
