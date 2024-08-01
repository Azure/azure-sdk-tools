import {  SourceFile, SyntaxKind } from "ts-morph";
import path from "path";
import { ApiVersionType } from "../../common/types";
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import { getTsSourceFile } from "../../common/utils";
import { findRestClientPath, getApiVersionTypeFromRestClient } from "../../xlc/apiVersion/utils";

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
    const relativeRestSrcFolder = "src/rest/";
    const typeFromClient = getApiVersionTypeFromRestClient(packageRoot, relativeRestSrcFolder, findRestClientPath);
    if (typeFromClient !== ApiVersionType.None) return typeFromClient;
    const typeFromOperations = getApiVersionTypeFromOperations(packageRoot);
    if (typeFromOperations !== ApiVersionType.None) return typeFromOperations;
    return ApiVersionType.Stable;
};
