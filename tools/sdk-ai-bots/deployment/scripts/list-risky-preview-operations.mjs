#!/usr/bin/env node

import { readFileSync } from "node:fs";

const inputPath = process.argv[2];
if (!inputPath) {
    console.error("Usage: node list-risky-preview-operations.mjs <azd-preview.json> [--all]");
    process.exit(1);
}
const includeCreates = process.argv.includes("--all");

const input = readFileSync(inputPath, "utf8");

function parseJsonStream(text) {
    const documents = [];
    let start = -1;
    let depth = 0;
    let inString = false;
    let escaped = false;

    for (let index = 0; index < text.length; index += 1) {
        const character = text[index];
        if (start === -1) {
            if (/\s/.test(character)) {
                continue;
            }
            if (character !== "{" && character !== "[") {
                throw new Error(`Unexpected content at offset ${index}`);
            }
            start = index;
            depth = 1;
            continue;
        }

        if (inString) {
            if (escaped) {
                escaped = false;
            } else if (character === "\\") {
                escaped = true;
            } else if (character === '"') {
                inString = false;
            }
            continue;
        }

        if (character === '"') {
            inString = true;
        } else if (character === "{" || character === "[") {
            depth += 1;
        } else if (character === "}" || character === "]") {
            depth -= 1;
            if (depth === 0) {
                documents.push(JSON.parse(text.slice(start, index + 1)));
                start = -1;
            }
        }
    }

    if (start !== -1 || inString) {
        throw new Error("Incomplete JSON document in azd preview output");
    }
    return documents;
}

const riskyOperations = new Set();

function visit(value) {
    if (Array.isArray(value)) {
        for (const child of value) {
            visit(child);
        }
        return;
    }
    if (!value || typeof value !== "object") {
        return;
    }

    const operation = value.Operation ?? value.operation;
    if (operation === "Delete" || operation === "Modify" || (includeCreates && operation === "Create")) {
        const resourceType = value.Type ?? value.type ?? "resource";
        const name = value.Name ?? value.name ?? "unknown";
        riskyOperations.add(`${operation}: ${resourceType}/${name}`);
    }

    for (const child of Object.values(value)) {
        visit(child);
    }
}

for (const document of parseJsonStream(input)) {
    visit(document);
}

for (const operation of [...riskyOperations].sort()) {
    console.log(operation);
}