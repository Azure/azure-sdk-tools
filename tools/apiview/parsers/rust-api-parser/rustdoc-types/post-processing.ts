import ts from 'typescript';
import * as fs from 'fs';

// Read the TypeScript file
const fileName = './output/typeshare-result.ts';
const outputFileName = './output/rustdoc-types.ts';
const sourceCode = fs.readFileSync(fileName, 'utf8');

// Parse the file
const sourceFile = ts.createSourceFile(fileName, sourceCode, ts.ScriptTarget.Latest, true);

// Function to modify the AST
function modifyTypes(node: ts.Node, context: ts.TransformationContext): ts.Node {
    if (ts.isTypeLiteralNode(node)) {
        return ts.factory.updateTypeLiteralNode(
            node,
            ts.factory.createNodeArray(
                node.members.map(member => {
                    // Check if the member is a property signature and has a name
                    if (ts.isPropertySignature(member) && member.name && ts.isIdentifier(member.name)) {
                        if (member.name.text === 'content') return undefined;
                        if (member.name.text === 'type') {
                            const typeName = (member.type as ts.LiteralTypeNode).literal as ts.StringLiteral;
                            const contentMember = node.members.find(m => ts.isPropertySignature(m) && ts.isIdentifier(m.name) && m.name.text === 'content');

                            if (contentMember) {
                                if (ts.isTypeNode((contentMember as ts.PropertySignature).type) && (contentMember as ts.PropertySignature).type.kind === ts.SyntaxKind.UndefinedKeyword) {
                                    console.log('Found undefined type', typeName.text, (contentMember as ts.PropertySignature).type);
                                    return ts.factory.createStringLiteral(
                                        typeName.text,
                                        false
                                    )
                                }
                                return ts.factory.createPropertySignature(
                                    undefined,
                                    ts.factory.createStringLiteral(typeName.text),
                                    undefined,
                                    (contentMember as ts.PropertySignature).type
                                );
                            }
                        }
                    }
                    return member;
                }).filter((member): member is ts.TypeElement => member !== undefined)
            )
        );
    }
    // Recursively visit each child node
    return ts.visitEachChild(node, childNode => modifyTypes(childNode, context), context);
}

// Modify the AST
const modifiedSourceFile = ts.transform(sourceFile, [context => rootNode => ts.visitEachChild(rootNode, childNode => modifyTypes(childNode, context), context)]).transformed[0] as ts.SourceFile;

// Print the modified AST
const printer = ts.createPrinter();
const modifiedCode = printer.printFile(modifiedSourceFile);

// Write the modified code back to the file
fs.writeFileSync(outputFileName, modifiedCode);

console.log('Types modified successfully!');