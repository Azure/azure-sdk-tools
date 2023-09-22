import {Project, ScriptTarget} from "ts-morph";

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