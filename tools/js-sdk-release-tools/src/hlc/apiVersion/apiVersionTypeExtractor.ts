import { ApiVersionType } from "../../common/types"
import { IApiVersionTypeExtractor } from "../../common/interfaces";
import { getClassicClientParametersPath, getTsSourceFile } from "../../common/utils";

// TODO: add unit test
export const getApiVersionType: IApiVersionTypeExtractor = async (packageRoot: string): Promise<ApiVersionType> => {
    const paraPath = getClassicClientParametersPath(packageRoot);
    const source = getTsSourceFile(paraPath);
    const variableDeclarations = source?.getVariableDeclarations();
    if (!variableDeclarations) return ApiVersionType.Stable;
    for (const variableDeclaration of variableDeclarations) {
        const fullText = variableDeclaration.getFullText();
        if (fullText.toLowerCase().includes('apiversion')) {
            const match = fullText.match(/defaultValue: "([0-9a-z-]+)"/);
            if (!match || match.length !== 2) {
                continue;
            }
            if (match[1].includes('preview')) {
                return ApiVersionType.Preview;
            }
        }
    }
    return ApiVersionType.Stable;
}