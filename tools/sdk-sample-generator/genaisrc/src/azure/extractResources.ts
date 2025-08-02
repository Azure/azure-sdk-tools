import ts from "typescript";
import * as fs from "node:fs/promises";
import { clientToResource } from "./resourceMap.ts";

/**
 * Given a file path, parse and return all Azure‐SDK instantiations.
 */
export async function discoverResourcesInFile(
    filePath: string,
): Promise<DiscoveredResource[]> {
    const code = await fs.readFile(filePath, "utf-8");
    return discoverResourcesInCode(code, filePath);
}

export interface ArgInfo {
    /** the exact source text */
    text: string;
    /** the TS node kind, e.g. "StringLiteral", "Identifier", etc. */
    kind: string;
    /** the inferred TS type at this location */
    type: string;
    /** for identifiers, the original initializer expression text */
    originalExpression?: string;
    /** if initializer references an imported symbol, the module specifier */
    packageName?: string;
    /** if argument or initializer reads an environment variable, its name */
    envVarName?: string;
}

export interface DiscoveredResource {
    clientClass: string;
    packageName: string;
    args: ArgInfo[];
}

function findImportedPackage(
    expr: ts.Expression,
    importMap: Record<string, string>,
): string | undefined {
    if (ts.isIdentifier(expr)) {
        return importMap[expr.text];
    }
    if (
        ts.isPropertyAccessExpression(expr) ||
        ts.isElementAccessExpression(expr)
    ) {
        return findImportedPackage(expr.expression as ts.Expression, importMap);
    }
    if (ts.isNewExpression(expr) && ts.isIdentifier(expr.expression)) {
        return importMap[expr.expression.text];
    }
    if (ts.isCallExpression(expr)) {
        return findImportedPackage(expr.expression as ts.Expression, importMap);
    }
    return undefined;
}

function findEnvVarName(expr: ts.Expression): string | undefined {
    if (
        ts.isPropertyAccessExpression(expr) &&
        ts.isPropertyAccessExpression(expr.expression) &&
        ts.isIdentifier(expr.expression.expression) &&
        expr.expression.expression.text === "process" &&
        expr.expression.name.text === "env"
    ) {
        return expr.name.text;
    }
    if (
        ts.isElementAccessExpression(expr) &&
        ts.isPropertyAccessExpression(expr.expression) &&
        ts.isIdentifier(expr.expression.expression) &&
        expr.expression.expression.text === "process" &&
        expr.expression.name.text === "env" &&
        ts.isStringLiteral(expr.argumentExpression!)
    ) {
        return expr.argumentExpression!.text;
    }
    if (ts.isCallExpression(expr)) {
        // check arguments first
        for (const arg of expr.arguments) {
            const name = findEnvVarName(arg);
            if (name) return name;
        }
        // then check the called expression
        return findEnvVarName(expr.expression as ts.Expression);
    }
    if (
        ts.isPropertyAccessExpression(expr) ||
        ts.isElementAccessExpression(expr)
    ) {
        return findEnvVarName(expr.expression as ts.Expression);
    }
    return undefined;
}

export function discoverResourcesInCode(
    code: string,
    fileName = "file.ts",
): DiscoveredResource[] {
    const compilerOptions: ts.CompilerOptions = {
        target: ts.ScriptTarget.ESNext,
        strict: true,
        moduleResolution: ts.ModuleResolutionKind.NodeNext,
        noUnusedParameters: true,
        noUnusedLocals: true,
        noFallthroughCasesInSwitch: true,
        esModuleInterop: true,
    };
    const host = ts.createCompilerHost(compilerOptions);
    const originalRead = host.readFile;
    host.readFile = (f) => (f === fileName ? code : originalRead(f));
    const originalFileExists = host.fileExists;
    host.fileExists = (f) => (f === fileName ? true : originalFileExists(f));

    const program = ts.createProgram([fileName], compilerOptions, host);
    const checker = program.getTypeChecker();
    const sourceFile = program.getSourceFile(fileName)!;

    const importMap: Record<string, string> = {};
    function collectImports(node: ts.Node) {
        if (ts.isImportDeclaration(node) && node.importClause) {
            const moduleName = (node.moduleSpecifier as ts.StringLiteral).text;
            const { name, namedBindings } = node.importClause;
            if (name) {
                importMap[name.text] = moduleName;
            }
            if (namedBindings) {
                if (ts.isNamedImports(namedBindings)) {
                    for (const spec of namedBindings.elements) {
                        importMap[spec.name.text] = moduleName;
                    }
                } else if (ts.isNamespaceImport(namedBindings)) {
                    importMap[namedBindings.name.text] = moduleName;
                }
            }
        }
        ts.forEachChild(node, collectImports);
    }
    collectImports(sourceFile);

    const functionReturnMap: Record<
        string,
        { node: ts.Expression; text: string }
    > = {};
    function collectFunctionReturns(node: ts.Node) {
        if (ts.isFunctionDeclaration(node) && node.name && node.body) {
            for (const stmt of node.body.statements) {
                if (ts.isReturnStatement(stmt) && stmt.expression) {
                    functionReturnMap[node.name.text] = {
                        node: stmt.expression,
                        text: stmt.expression.getText(sourceFile),
                    };
                    break;
                }
            }
        }
        ts.forEachChild(node, collectFunctionReturns);
    }
    collectFunctionReturns(sourceFile);

    const varInitMap: Record<string, { node: ts.Expression; text: string }> =
        {};
    function collectInits(node: ts.Node) {
        if (
            ts.isVariableDeclaration(node) &&
            ts.isIdentifier(node.name) &&
            node.initializer
        ) {
            varInitMap[node.name.text] = {
                node: node.initializer,
                text: node.initializer.getText(sourceFile),
            };
        }
        ts.forEachChild(node, collectInits);
    }
    collectInits(sourceFile);

    const results: DiscoveredResource[] = [];

    function visit(node: ts.Node) {
        if (ts.isNewExpression(node) && ts.isIdentifier(node.expression)) {
            const className = node.expression.text;
            if (className in clientToResource) {
                const clientPackage = importMap[className];
                const args: ArgInfo[] = (node.arguments || []).map((arg) => {
                    const kind = ts.SyntaxKind[arg.kind];
                    const tsType = checker.getTypeAtLocation(arg);
                    const typeString = checker.typeToString(tsType);

                    const info: ArgInfo = {
                        text: arg.getText(sourceFile),
                        kind,
                        type: typeString,
                    };

                    if (
                        ts.isCallExpression(arg) &&
                        ts.isIdentifier(arg.expression)
                    ) {
                        const fn = arg.expression.text;
                        const ret = functionReturnMap[fn];
                        if (ret) {
                            // if return is a variable, unwrap its init
                            if (
                                ts.isIdentifier(ret.node) &&
                                ret.node.text in varInitMap
                            ) {
                                const init = varInitMap[ret.node.text];
                                info.originalExpression = init.text;
                                const ev = findEnvVarName(init.node);
                                if (ev) info.envVarName = ev;
                                const pkg = findImportedPackage(
                                    init.node,
                                    importMap,
                                );
                                if (pkg) info.packageName = pkg;
                            } else {
                                info.originalExpression = ret.text;
                                const ev = findEnvVarName(ret.node);
                                if (ev) info.envVarName = ev;
                                const pkg = findImportedPackage(
                                    ret.node,
                                    importMap,
                                );
                                if (pkg) info.packageName = pkg;
                            }
                            let name = fn.startsWith("get") ? fn.slice(3) : fn;
                            name = name.charAt(0).toLowerCase() + name.slice(1);
                            info.text = name;
                            info.kind = "Identifier";
                        }
                    }

                    if (ts.isIdentifier(arg)) {
                        const init = varInitMap[arg.text];
                        if (init) {
                            info.originalExpression = init.text;
                            info.envVarName = findEnvVarName(init.node);
                            info.packageName = findImportedPackage(
                                init.node,
                                importMap,
                            );
                        }
                    }
                    info.envVarName ??= findEnvVarName(arg);
                    info.packageName ??= findImportedPackage(arg, importMap);

                    return info;
                });

                results.push({
                    clientClass: className,
                    packageName: clientPackage,
                    args,
                });
            }
        }
        ts.forEachChild(node, visit);
    }

    visit(sourceFile);
    return results;
}
