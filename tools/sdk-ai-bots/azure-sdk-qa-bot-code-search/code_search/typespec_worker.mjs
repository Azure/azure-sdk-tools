import { SyntaxKind, parse } from "@typespec/compiler/ast";
import readline from "node:readline";

const declarationKinds = new Map([
    [SyntaxKind.NamespaceStatement, "namespace"],
    [SyntaxKind.ModelStatement, "model"],
    [SyntaxKind.ScalarStatement, "scalar"],
    [SyntaxKind.InterfaceStatement, "interface"],
    [SyntaxKind.OperationStatement, "operation"],
    [SyntaxKind.EnumStatement, "enum"],
    [SyntaxKind.UnionStatement, "union"],
    [SyntaxKind.AliasStatement, "alias"],
    [SyntaxKind.DecoratorDeclarationStatement, "decorator"],
    [SyntaxKind.FunctionDeclarationStatement, "function"],
]);

function lineNumber(lineStarts, offset) {
    let low = 0;
    let high = lineStarts.length;
    while (low + 1 < high) {
        const middle = Math.floor((low + high) / 2);
        if (lineStarts[middle] <= offset) {
            low = middle;
        } else {
            high = middle;
        }
    }
    return low + 1;
}

function parseSource(source) {
    const script = parse(source);
    if (script.parseDiagnostics.length > 0) {
        throw new Error(
            script.parseDiagnostics
                .map((diagnostic) => `${diagnostic.code}: ${diagnostic.message}`)
                .join("; "),
        );
    }
    const lineStarts = [0];
    for (let index = 0; index < source.length; index += 1) {
        if (source[index] === "\n") {
            lineStarts.push(index + 1);
        }
    }
    const declarations = [];
    function addStatements(statements) {
        if (!Array.isArray(statements)) {
            return;
        }
        for (const statement of statements) {
            declarations.push({
                start: statement.pos,
                end: statement.end,
                start_line: lineNumber(lineStarts, statement.pos),
                end_line: lineNumber(lineStarts, Math.max(statement.pos, statement.end - 1)),
                name: statement.id?.sv ?? statement.target?.sv ?? null,
                kind: declarationKinds.get(statement.kind) ?? "statement",
            });
            addStatements(statement.statements);
            addStatements(statement.operations);
        }
    }
    addStatements(script.statements);
    return declarations;
}

const input = readline.createInterface({
    input: process.stdin,
    crlfDelay: Infinity,
});

for await (const line of input) {
    let id = null;
    try {
        const request = JSON.parse(line);
        id = request.id;
        const declarations = parseSource(request.source);
        process.stdout.write(`${JSON.stringify({ id, declarations })}\n`);
    } catch (error) {
        process.stdout.write(
            `${JSON.stringify({ id, error: error instanceof Error ? error.message : String(error) })}\n`,
        );
    }
}
