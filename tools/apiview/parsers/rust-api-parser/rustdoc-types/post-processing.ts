import { Project, SyntaxKind } from "ts-morph";
import * as fs from 'fs';
import * as path from 'path';

// Initialize the project with the specified tsconfig file
const project = new Project({ tsConfigFilePath: "../tsconfig.json" });

// Get the source file from the project
const sourceFile = project.getSourceFileOrThrow("output/typeshare-result.ts");

// Create a new source file for the output
const newSourceFile = project.createSourceFile("output/rustdoc-types.ts", "", { overwrite: true });

// Read the lib.rs file
const libRsPath = path.join(__dirname, 'lib.rs');
const libRsContent = fs.readFileSync(libRsPath, 'utf8');

// Extract the FORMAT_VERSION line
const formatVersionMatch = libRsContent.match(/pub const FORMAT_VERSION: u32 = (\d+);/);
if (formatVersionMatch) {
    const formatVersion = formatVersionMatch[1];
    const formatVersionLine = `export const FORMAT_VERSION = ${formatVersion};`;
    // Add the transformed FORMAT_VERSION line to the new source file
    newSourceFile.addStatements(formatVersionLine);
}

// Process all type aliases in the source file
sourceFile.getTypeAliases().forEach(typeAlias => {
    // Get the type node from the type alias
    const typeNode = typeAlias.getTypeNodeOrThrow();

    // Check if the type node is a union type
    if (typeNode.isKind(SyntaxKind.UnionType)) {
        const unionType = typeNode.asKindOrThrow(SyntaxKind.UnionType);

        // Map the new types from the union type nodes
        const newTypes = unionType.getTypeNodes().map(typeNode => {
            // Check if the type node is a type literal
            const typeLiteral = typeNode.asKind(SyntaxKind.TypeLiteral);
            if (typeLiteral) {
                // Get the properties of the type literal
                const properties = typeLiteral.getProperties();
                const typeProperty = properties.find(prop => prop.getName() === "type");
                const contentProperty = properties.find(prop => prop.getName() === "content");

                // If both type and content properties are found
                if (typeProperty && contentProperty) {
                    const typeValue = typeProperty.getTypeNodeOrThrow().getText();
                    // Check if the content property is undefined
                    if (contentProperty.getType().isUndefined()) {
                        return `${typeValue}`;
                    } else {
                        const contentType = contentProperty.getTypeNodeOrThrow().getText();
                        return `{${typeValue}: ${contentType}}`;
                    }
                }
            }
            // Return the text of the type node if it's not a type literal or properties are not found
            return typeNode.getText();
        });

        // Join the new types into a single union type string
        const newUnionType = newTypes.join("\n| ");

        // Add the updated type alias to the new source file
        newSourceFile.addTypeAlias({
            name: typeAlias.getName(),
            type: newUnionType,
            isExported: true,
        });
    } else {
        // If it's not a union type, copy the type alias as is
        newSourceFile.addTypeAlias({
            name: typeAlias.getName(),
            type: typeNode.getText(),
            isExported: true,
        });
    }
});

// Add the property `inputs: Array<[string, Type]>;` to the interface `FunctionSignature`
const functionSignatureInterface = sourceFile.getInterfaceOrThrow("FunctionSignature");
functionSignatureInterface.addProperty({
    name: "inputs",
    type: "Array<[string, Type]>",
    hasQuestionToken: false
});

// Copy everything else from the input file to the new source file
sourceFile.getStatements().forEach(statement => {
    if (!statement.isKind(SyntaxKind.TypeAliasDeclaration) 
        && !(statement.isKind(SyntaxKind.InterfaceDeclaration) && statement.getName() === "FunctionSignature")) {
        newSourceFile.addStatements(statement.getText());
    }
});

// Add the updated `FunctionSignature` interface to the new source file
newSourceFile.addInterface({
    name: functionSignatureInterface.getName(),
    properties: functionSignatureInterface.getProperties().map(prop => ({
        name: prop.getName(),
        type: prop.getTypeNodeOrThrow().getText(),
        hasQuestionToken: prop.hasQuestionToken()
    })),
    isExported: true
});

newSourceFile.formatText();
// Save the new source file
newSourceFile.saveSync();