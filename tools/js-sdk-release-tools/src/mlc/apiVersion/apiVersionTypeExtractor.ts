import {  SourceFile, SyntaxKind } from "ts-morph";
import shell from "shelljs";
import path from "path";
import * as ts from "typescript";

import { ApiVersionType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import { getTsSourceFile } from "../../common/utils";
import { readFileSync } from "fs";

const findRestClientPath = (packageRoot: string): string => {
    const restPath = path.join(packageRoot, "src/rest/");
    const fileNames = shell.ls(restPath);
    const clientFiles = fileNames.filter((f) => f.endsWith("Client.ts"));
    if (clientFiles.length !== 1)
        throw new Error(`Single client is supported, but found "${clientFiles}" in ${restPath}`);

    const clientPath = path.join(restPath, clientFiles[0]);
    return clientPath;
};

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

const getApiVersionTypeFromRestClient: IApiVersionTypeExtractor = (
    packageRoot: string
): ApiVersionType => {
    const clientPath = findRestClientPath(packageRoot);
    const apiVersion = findApiVersionInRestClient(clientPath);
    if (apiVersion && apiVersion.indexOf("-preview") >= 0)
        return ApiVersionType.Preview;
    if (apiVersion && apiVersion.indexOf("-preview") < 0)
        return ApiVersionType.Stable;
    return ApiVersionType.None;
};

const findApiVersionsInOperations = (
    sourceFile: SourceFile | undefined
): Array<string> | undefined => {
    const interfaces = sourceFile?.getInterfaces();
    const interfacesWithApiVersion = interfaces?.filter((itf) =>
        itf.getProperty('"api-version"')
    );
    const apiVersions = interfacesWithApiVersion?.map((itf) => {
        const property = itf.getMembers().filter((m) => {
            const defaultValue = m.getChildrenOfKind(
                SyntaxKind.StringLiteral
            )[0];
            return defaultValue && defaultValue.getText() === '"api-version"';
        })[0];
        const apiVersion = property
            .getChildrenOfKind(SyntaxKind.LiteralType)[0]
            .getText();
        return apiVersion;
    });
    return apiVersions;
};

const getApiVersionTypeFromOperations: IApiVersionTypeExtractor = (
    packageRoot: string
): ApiVersionType => {
    const paraPath = path.join(packageRoot, "src/rest/parameters.ts");
    const sourceFile = getTsSourceFile(paraPath);
    const apiVersions = findApiVersionsInOperations(sourceFile);
    if (!apiVersions) return ApiVersionType.None;
    const previewVersions = apiVersions.filter(
        (v) => v.indexOf("-preview") >= 0
    );
    return previewVersions.length > 0
        ? ApiVersionType.Preview
        : ApiVersionType.Stable;
};

export const getApiVersionType: IApiVersionTypeExtractor = (
    packageRoot: string
): ApiVersionType => {
    const typeFromClient = getApiVersionTypeFromRestClient(packageRoot);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    const typeFromOperations = getApiVersionTypeFromOperations(packageRoot);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
};
