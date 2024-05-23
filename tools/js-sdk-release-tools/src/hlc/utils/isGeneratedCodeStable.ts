import {Project, ScriptTarget} from "ts-morph";
import shell from 'shelljs';
import path from 'path';

import {CodeGenLevel} from "../../common/types"
import {DEFAULT_API_VERSION} from "../../common/constants"

function getClassicClientParametersPath(packageRoot: string): string {
    return path.join(packageRoot, 'src', 'models', 'parameters.ts');
} 

function getCodeGenLevel(parametersPath: string) : CodeGenLevel {
    const exist = shell.test('-e', parametersPath);
    const level = exist ? CodeGenLevel.Classic : CodeGenLevel.Modular;
    console.log(`CodeGen Level: ${level} detected`);
    return level;
}

// TODO
function findApiVersionFromRestClient(packageRoot: string): string | null {return null;}
    
// TODO
function findApiVersionFromOperations(packageRoot: string): string | null {
        const paraPath = path.join(packageRoot, 'src/rest/parameters.ts');
        return null;
    }

function findModularClientApiVersion(packageRoot: string): string | null {
    const clientApiVersion = findApiVersionFromRestClient(packageRoot);
    if (clientApiVersion) return clientApiVersion;
    
    const operationApiVersion = findApiVersionFromOperations(packageRoot);
    return operationApiVersion;
}

function findClassicClientApiVersion(packageRoot: string): string {
    const project = new Project({
        compilerOptions: {
            target: ScriptTarget.ES2015,
        },
    });
    const paraPath = getClassicClientParametersPath(packageRoot);
    project.addSourceFileAtPath(paraPath);
    const source = project.getSourceFile(paraPath);
    const variableDeclarations = source?.getVariableDeclarations();
    if (!variableDeclarations) return DEFAULT_API_VERSION;
    for (const variableDeclaration of variableDeclarations) {
        const fullText = variableDeclaration.getFullText();
        if (fullText.toLowerCase().includes('apiversion')) {
            const match = fullText.match(/defaultValue: "([0-9a-z-]+)"/);
            if (!match || match.length !== 2) {
                continue;
            }
            return match[1];
        }
    }
    return DEFAULT_API_VERSION;
}

export function isGeneratedCodeStable(filePath: string) {
    const project = new Project({
        compilerOptions: {
            target: ScriptTarget.ES2015,
        },
    });
    project.addSourceFileAtPath(filePath);
    const source = project.getSourceFile(filePath);
    const variableDeclarations = source?.getVariableDeclarations();
    if (!variableDeclarations) return true;
    for (const variableDeclaration of variableDeclarations) {
        const fullText = variableDeclaration.getFullText();
        if (fullText.toLowerCase().includes('apiversion')) {
            const match = fullText.match(/defaultValue: "([0-9a-z-]+)"/);
            if (!match || match.length !== 2) {
                continue;
            }
            if (match[1].includes('preview')) {
                return false;
            }
        }
    }
    return true;
}

function getClientApiVersion(packageRoot: string) {
    const path = getClassicClientParametersPath(packageRoot);
    const codeGenLevel = getCodeGenLevel(path);
    return codeGenLevel == CodeGenLevel.Classic ?
        findClassicClientApiVersion(packageRoot) :
        findModularClientApiVersion(packageRoot);
}