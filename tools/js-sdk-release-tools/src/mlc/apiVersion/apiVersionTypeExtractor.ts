import { SourceFile, SyntaxKind } from "ts-morph";
import shell from 'shelljs';
import path from 'path';

import { ApiVersionType } from "../../common/types"
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import { getTsSourceFile } from "../../common/utils";

const findRestClientPath = (packageRoot: string): string => {
    const restPath = path.join(packageRoot, 'src/rest/');
    const fileNames = shell.ls(restPath);
    const clientFiles = fileNames.filter(f => f.endsWith("Client.ts"));
    if (clientFiles.length !== 1) throw new Error(`Single client is supported, but found ${clientFiles}`);

    const clientPath = path.join(restPath, clientFiles[0]);
    return clientPath;
};

const matchPattern = (text: string, pattern: RegExp): string | undefined => {
    const match = text.match(pattern);
    const found = match != null && match.length === 2;
    return found ? match?.at(1) : undefined;
}

const findApiVersionInRestClient = (clientPath: string): string | undefined => {
    const sourceFile = getTsSourceFile(clientPath);
    const createClientFunction = sourceFile?.getFunction("createClient");
    if (!createClientFunction) throw new Error("Function 'createClient' not found.");

    const apiVersionStatements = createClientFunction.getStatements()
        .filter(s =>
            s.getKind() === SyntaxKind.ExpressionStatement &&
            s.getText().indexOf("options.apiVersion") > -1);
    if (apiVersionStatements.length === 0) return undefined;

    const text = apiVersionStatements[apiVersionStatements.length - 1].getText();
    const pattern = /(\d{4}-\d{2}-\d{2}(?:-preview)?)/;
    const apiVersion = matchPattern(text, pattern);
    return apiVersion;
};

const getApiVersionTypeFromRestClient: IApiVersionTypeExtractor = (packageRoot: string): ApiVersionType => {
    const clientPath = findRestClientPath(packageRoot);
    const apiVersion = findApiVersionInRestClient(clientPath);
    if (apiVersion && apiVersion.indexOf("-preview") >= 0) return ApiVersionType.Preview;
    if (apiVersion && apiVersion.indexOf("-preview") < 0) return ApiVersionType.Stable;
    return ApiVersionType.None;
};

const findApiVersionsInOperations = (sourceFile: SourceFile | undefined): Array<string> | undefined => {
    const interfaces = sourceFile?.getInterfaces();
    const interfacesWithApiVersion = interfaces?.filter(itf => itf.getProperty('"api-version"'));
    const apiVersions = interfacesWithApiVersion?.map(itf => {
        const property = itf.getMembers()
            .filter(m => {
                const defaultValue = m.getChildrenOfKind(SyntaxKind.StringLiteral)[0];
                return defaultValue && defaultValue.getText() === '"api-version"';
            })[0];
        const apiVersion = property.getChildrenOfKind(SyntaxKind.LiteralType)[0].getText();
        return apiVersion;
    });
    return apiVersions;
}

const getApiVersionTypeFromOperations: IApiVersionTypeExtractor = (packageRoot: string): ApiVersionType => {
    const paraPath = path.join(packageRoot, 'src/rest/parameters.ts');
    const sourceFile = getTsSourceFile(paraPath);
    const apiVersions = findApiVersionsInOperations(sourceFile);
    if (!apiVersions) return ApiVersionType.None;
    const previewVersions = apiVersions.filter(v => v.indexOf("-preview") >= 0);
    return previewVersions.length > 0 ? ApiVersionType.Preview : ApiVersionType.Stable;
};

// TODO: add unit test
export const getApiVersionType: IApiVersionTypeExtractor = (packageRoot: string): ApiVersionType => {
    const typeFromClient = getApiVersionTypeFromRestClient(packageRoot);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    const typeFromOperations = getApiVersionTypeFromOperations(packageRoot);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
}
