import { readFileSync } from "node:fs";
import { getTsSourceFile } from "../../common/utils";
import { ApiVersionType, SDKType } from "../../common/types";
import ts from "typescript";
import path from "node:path";
import shell from "shelljs";
import { SourceFile, SyntaxKind } from "ts-morph";
import { logger } from "../../utils/logger";
import { glob } from 'glob'

var unixify = require('unixify');

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

// workaround for createClient function changes it's way to setup api-version
export const findApiVersionInRestClient = (clientPath: string): string | undefined => {
    const version2 = findApiVersionInRestClientV2(clientPath);
    if (version2) {
        return version2;
    }
    const version1 = findApiVersionInRestClientV1(clientPath);
    return version1;
};

export const tryFindRestClientPath = async (packageRoot: string, clientPattern: string): Promise<string | undefined> => {
    const pattern = unixify(path.join(packageRoot, clientPattern));
    const clientFiles = await glob(pattern);
    if (clientFiles.length !== 1) {
        logger.warn(`Failed to find extactly one REST client in pattern '${pattern}', got '${clientFiles}'.`);
        return undefined;
    }
    return clientFiles[0];
}; 

export const findParametersPath = (packageRoot: string, relativeParametersFolder: string): string => {
    const parametersPath = path.join(packageRoot, relativeParametersFolder);
    const fileNames = shell.ls(parametersPath);
    const clientFiles = fileNames.filter((f) => f === "parameters.ts");
    if (clientFiles.length !== 1)
        throw new Error(`Expected 1 'parameters.ts' file, but found '${clientFiles}' in '${parametersPath}'.`);

    const clientPath = path.join(parametersPath, clientFiles[0]);
    return clientPath;
};

export const getApiVersionTypeFromRestClient = async (
    packageRoot: string,
    clientPattern: string,
    findRestClientPath: (packageRoot: string, clientPattern: string) => Promise<string | undefined>
): Promise<ApiVersionType> => {
    const clientPath = await findRestClientPath(packageRoot, clientPattern);
    if (!clientPath) return ApiVersionType.None;
    const apiVersion = findApiVersionInRestClient(clientPath);
    if (apiVersion && apiVersion.indexOf("-preview") >= 0)
        return ApiVersionType.Preview;
    if (apiVersion && apiVersion.indexOf("-preview") < 0)
        return ApiVersionType.Stable;
    return ApiVersionType.None;
};

export const getApiVersionTypeFromOperations = (
    packageRoot: string,
    relativeParametersFolder: string,
    findPararametersPath: (packageRoot: string, relativeParametersFolder: string) => string
): ApiVersionType => {
    const paraPath = findPararametersPath(packageRoot, relativeParametersFolder);
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