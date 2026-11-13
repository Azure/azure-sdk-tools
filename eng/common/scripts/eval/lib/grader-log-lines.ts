import { stripVTControlCharacters } from "node:util";

export const SOURCE_LOG_LINES_FILE = "source-log-lines.json";
const MAX_LOG_LINES = 200_000;
const MAX_COMPARISONS = 1_000_000;
const STATUS = /^[\u2713\u2714\u2717\u2718]\s/;
const clean = value => stripVTControlCharacters(String(value)).replace(/^\d{4}-\d{2}-\d{2}T[^ ]+Z /, "").trim();
const evidenceText = value => clean(value).replace(/\s+/g, " ");
const grades = record => record.gradeResult?.details ?? record.grade?.details ?? [];
const recordKey = record => JSON.stringify([record.evalName, record.stimulus, record.model]);

export function taskLogPrefixLineCount(provider, sourceLog, logId) {
    const matches = provider?.tasks?.filter(task => task.id === sourceLog.taskId && task.parentId === sourceLog.jobId);
    const task = matches?.length === 1 ? matches[0] : null;
    const dependencies = provider?.jobDependencies?.[sourceLog.jobId] ?? [];
    if (!task || task.state !== 2 || task.result === 4 || task.logId !== logId ||
        !provider.jobs?.some(job => job.id === sourceLog.jobId) || !Array.isArray(dependencies) ||
        dependencies.some(value => typeof value !== "string") ||
        [task.condition, task.originalCondition, task.message].some(value => value != null && typeof value !== "string")) return undefined;
    const countBreaks = value => String(value ?? "").split("\n").length - 1;
    if (task.condition && task.originalCondition && countBreaks(task.condition) !== countBreaks(task.originalCondition)) return undefined;
    if (/variables[.\[]/i.test(task.condition ?? "") && Object.values(provider.variables ?? {})
        .some(value => /[\r\n]/.test(String(value?.value ?? value ?? "")))) return undefined;
    let count = 4;
    if (task.condition) count += 3 + countBreaks(task.originalCondition || task.condition);
    if (task.message) count += 3 + countBreaks(task.message);
    for (const value of [task.condition, task.originalCondition, task.message]) if (/##\[/.test(value ?? "")) return undefined;
    count += dependencies.length ? dependencies.reduce((total, dependency) => {
        const name = provider.phases?.find(phase => phase.refName === dependency)?.name ?? dependency;
        return total + 1 + countBreaks(name);
    }, 0) : 1;
    return count <= MAX_LOG_LINES ? count : undefined;
}

export function taskLogDisplayLines(text, prefixLineCount) {
    if (typeof text !== "string" || !Number.isSafeInteger(prefixLineCount) || prefixLineCount < 0 || prefixLineCount > MAX_LOG_LINES) return undefined;
    const result = Array(prefixLineCount).fill("");
    let grouped = false;
    for (const line of text.split(/\r?\n/)) {
        const value = clean(line);
        if (value.startsWith("##[group]")) grouped = true;
        else if (value === "##[endgroup]" && grouped) { grouped = false; continue; }
        result.push(line);
        if (result.length > MAX_LOG_LINES) return undefined;
    }
    return result;
}

function individualLines(record, block, lines) {
    const graderLines = Object.create(null), locations = new Map();
    for (const grade of grades(record)) {
        if (typeof grade.name !== "string" || typeof grade.passed !== "boolean") return null;
        const symbols = grade.passed ? ["\u2713", "\u2714"] : ["\u2717", "\u2718"];
        const matches = [];
        for (let index = block.start + 1; index < block.end; index++) {
            if (!symbols.some(symbol => {
                const label = symbol + " " + grade.name;
                return lines[index].startsWith(label) && /^(?: #[1-9][0-9]*)? {2}/.test(lines[index].slice(label.length));
            })) continue;
            let end = index + 1;
            while (end < block.end && !STATUS.test(lines[end]) && !/grader\(s\) failed|All graders passed/.test(lines[end])) end++;
            const evidence = evidenceText(lines.slice(index, end).join(" "));
            const expected = evidenceText(grade.evidence ?? "");
            if (!expected || evidence.includes(expected)) matches.push(index + 1);
        }
        if (!matches.length) return null;
        const existing = locations.get(grade.name) ?? []; existing.push({ passed: grade.passed, matches }); locations.set(grade.name, existing);
    }
    for (const [name, entries] of locations) {
        const failures = entries.filter(entry => !entry.passed), selected = failures.length ? failures : entries;
        const matches = new Set(selected.flatMap(entry => entry.matches));
        if (matches.size === 1) graderLines[name] = [...matches][0];
    }
    return locations.size ? { line: block.start + 1, graderLines } : null;
}

function groupedLines(records, block, lines) {
    const totals = new Map(), locations = new Map();
    for (const record of records) for (const grade of grades(record)) {
        if (typeof grade.name !== "string" || typeof grade.passed !== "boolean") continue;
        const value = totals.get(grade.name) ?? { passed: 0, total: 0 };
        value.passed += Number(grade.passed); value.total++;
        totals.set(grade.name, value);
    }
    for (let index = block.start + 1; index < block.end; index++) {
        const match = /^[\u2713\u2714\u2717\u2718]\s+(.+?)\s+(\d+)\/(\d+)$/.exec(lines[index]);
        if (!match) continue;
        const expected = totals.get(match[1]);
        if (!expected || expected.passed !== Number(match[2]) || expected.total !== Number(match[3])) continue;
        const existing = locations.get(match[1]);
        locations.set(match[1], existing === undefined ? index + 1 : null);
    }
    return Object.fromEntries([...locations].filter(([, line]) => line !== null));
}

export function mapGraderLogLines(records, displayedLogLines) {
    const result = Object.create(null);
    if (!Array.isArray(records) || !Array.isArray(displayedLogLines) || displayedLogLines.length > MAX_LOG_LINES) return result;
    const lines = displayedLogLines.map(clean), blocks = [], byKey = new Map(), trials = new Map();
    for (let index = 0; index < lines.length; index++) {
        const header = /^\u2501{3,}\s+(.+?)\s+\u00b7\s+(.+?)\s+\[([^\]]+)\]\s+\u2501{3,}$/.exec(lines[index]);
        if (!header) continue;
        if (blocks.length) blocks.at(-1).end = index;
        const block = { start: index, end: lines.length };
        blocks.push(block);
        const key = JSON.stringify(header.slice(1));
        const matches = byKey.get(key) ?? []; matches.push(block); byKey.set(key, matches);
    }
    for (const record of records) {
        if (typeof record.itemId !== "string" || !record.itemId || !Array.isArray(grades(record))) continue;
        const key = recordKey(record), group = trials.get(key) ?? []; group.push(record); trials.set(key, group);
    }
    let comparisons = 0;
    for (const [key, group] of trials) {
        const candidates = byKey.get(key) ?? [];
        if (candidates.length === 1 && group.length > 1) {
            const mapped = groupedLines(group, candidates[0], lines);
            if (Object.keys(mapped).length) {
                for (const record of group) {
                    const graderLines = Object.fromEntries(grades(record).filter(grade => mapped[grade.name] !== undefined)
                        .map(grade => [grade.name, mapped[grade.name]]));
                    if (Object.keys(graderLines).length) result[record.itemId] = { line: candidates[0].start + 1, graderLines };
                }
            }
            continue;
        }
        for (const record of group) {
            const matches = [];
            for (const block of candidates) {
                comparisons += (block.end - block.start) * grades(record).length;
                if (comparisons > MAX_COMPARISONS) return result;
                const match = individualLines(record, block, lines);
                if (match) matches.push(match);
            }
            if (matches.length === 1) result[record.itemId] = matches[0];
        }
    }
    return result;
}