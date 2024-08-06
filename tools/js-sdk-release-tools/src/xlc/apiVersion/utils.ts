import { readFileSync } from "node:fs";
import { getTsSourceFile } from "../../common/utils";
import { ApiVersionType } from "../../common/types";
import ts from "typescript";
import path from "node:path";
import shell from "shelljs";

const findApiVersionInRestClientV1 = (
    clientPath: string
): string | undefined => {
    const sourceFile = getTsSourceFile(clientPath);
    const createClientFunction = sourceFile?.getFunction("createClient");
    if (!createClientFunction)
        throw new Error("Function 'createClient' not found.");

    const apiVersionStatements = createClientFunction
        .getStatements()
        .filter((s) => s.getText().includes("options.apiVersion"));
    if (apiVersionStatements.length === 0) {
        return undefined;
    }
    const text =
        apiVersionStatements[apiVersionStatements.length - 1].getText();
    return extractApiVersionFromText(text);
};

const extractApiVersionFromText = (text: string): string | undefined => {
    const begin = text.indexOf('"');
    const end = text.lastIndexOf('"');
    return text.substring(begin + 1,  end);
};

// new ways in @autorest/typespec-ts emitter to set up api-version
const findApiVersionInRestClientV2 = (clientPath: string): string | undefined => {
    const sourceCode= readFileSync(clientPath, {encoding: 'utf-8'})
    const sourceFile = ts.createSourceFile("example.ts", sourceCode, ts.ScriptTarget.Latest, true);
    const createClientFunction = sourceFile.statements.filter(s => (s as ts.FunctionDeclaration)?.name?.escapedText === 'createClient').map(s => (s as ts.FunctionDeclaration))[0];
    let apiVersion: string | undefined = undefined;
    createClientFunction.parameters.forEach(p => {
        const isBindingPattern = node => node && typeof node === "object" && "elements" in node && "parent" in node && "kind" in node;
        if (!isBindingPattern(p.name)) {
            return;
        }
        const binding = p.name as ts.ObjectBindingPattern;
        const apiVersionTexts = binding.elements?.filter(e => (e.name as ts.Identifier)?.escapedText === "apiVersion").map(e => e.initializer?.getText());
        // apiVersionTexts.length must be 0 or 1, otherwise the binding pattern contains the same keys, which causes a ts error
        if (apiVersionTexts.length === 1 && apiVersionTexts[0]) {
            apiVersion = extractApiVersionFromText(apiVersionTexts[0]);
        }
    });
    return apiVersion;
};

// workaround for createClient function changes it's way to setup api-version
export const findApiVersionInRestClient = (clientPath: string): string | undefined => {
    const version2 = findApiVersionInRestClientV2(clientPath);
    if (version2) {
        return version2;
    }
    const version1 = findApiVersionInRestClientV1(clientPath);
    return version1;
};

export const findRestClientPath = (packageRoot: string, relativeRestSrcFolder: string): string => {
    const restPath = path.join(packageRoot, relativeRestSrcFolder);
    const fileNames = shell.ls(restPath);
    const clientFiles = fileNames.filter((f) => f.endsWith("Client.ts"));
    if (clientFiles.length !== 1)
        throw new Error(`Single client is supported, but found "${clientFiles}" in ${restPath}`);

    const clientPath = path.join(restPath, clientFiles[0]);
    return clientPath;
};

export const getApiVersionTypeFromRestClient = (
    packageRoot: string,
    relativeRestSrcFolder: string,
    findRestClientPath: (packageRoot: string, relativeRestSrcFolder: string) => string
): ApiVersionType => {
    const clientPath = findRestClientPath(packageRoot, relativeRestSrcFolder);
    const apiVersion = findApiVersionInRestClient(clientPath);
    if (apiVersion && apiVersion.indexOf("-preview") >= 0)
        return ApiVersionType.Preview;
    if (apiVersion && apiVersion.indexOf("-preview") < 0)
        return ApiVersionType.Stable;
    return ApiVersionType.None;
};