import { Project, ScriptTarget, SyntaxKind } from "ts-morph";
import shell from 'shelljs';
import path from 'path';

import { ApiVersionType } from "../../common/types"
import { IApiVersionTypeExtractor } from "../../common/interfaces";

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
    const target = ScriptTarget.ES2015;
    const compilerOptions = { target };
    const project = new Project({ compilerOptions });
    project.addSourceFileAtPath(clientPath);
    const sourceFile = project.getSourceFile(clientPath);

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

// TODO: not implemented: need a example
const getApiVersionTypeFromOperations: IApiVersionTypeExtractor = (packageRoot: string): ApiVersionType => {
    console.log('findApiVersionFromOperations')
    const paraPath = path.join(packageRoot, 'src/rest/parameters.ts');
    return ApiVersionType.Stable;
};

// TODO: add unit test
export const getApiVersionType: IApiVersionTypeExtractor = (packageRoot: string): ApiVersionType => {
    const typeFromClient = getApiVersionTypeFromRestClient(packageRoot);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    const typeFromOperations = getApiVersionTypeFromOperations(packageRoot);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
}
