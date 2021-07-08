// --------------------------------------------------------------------------
//
// Copyright (c) Microsoft Corporation. All rights reserved.
//
// The MIT License (MIT)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the ""Software""), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//
// --------------------------------------------------------------------------

import Foundation
import AST
import Parser
import Source

/// Handles the generation of APIView JSON files.
class APIViewManager {

    // MARK: Properties

    static var shared = APIViewManager()
    let args = CommandLineArguments()
    let tokenFile = TokenFile(name: "", tokens: [], navigation: [])

    // MARK: Methods

    func run() throws {
        guard let sourcePath = args.source else {
            SharedLogger.fail("usage error: SwiftAPIView --source PATH")
        }
        guard let sourceUrl = URL(string: sourcePath) else {
            SharedLogger.fail("usage error: `--source PATH` was invalid.")
        }

        try buildTokenFile(from: sourceUrl)

        let destPath = args.dest ?? "~/Desktop"
        guard let destUrl = URL(string: destPath) else {
            SharedLogger.fail("usage error: `--dest PATH` was invalid.")
        }
        // TODO: Output the token file
    }

    func buildTokenFile(from sourceUrl: URL) throws {
        // TODO: Build AST. Traverse and populate token file.
        SharedLogger.debug("URL: \(sourceUrl.absoluteString)")
        let sourceFile = try SourceReader.read(at: sourceUrl.absoluteString)
        let parser = Parser(source: sourceFile)
        let topLevelDecl = try parser.parse()
        let visitor = TokenVisitor()
        print(try visitor.traverse(topLevelDecl))
        
        
    }
}

struct TokenNode {
    var node : ASTNode
    var tokens : [TokenItem]
    var depth : Int
    
    init(node: ASTNode, tokens: [TokenItem], depth: Int) {
        self.node = node
        self.tokens = tokens
        self.depth = depth
    }
}

class TokenVisitor : ASTVisitor {
    
    var visited : [ASTNode] = []
    
    func registerVisit(to visitingNode : ASTNode) -> Bool {
        for node in visited {
            if node.sourceLocation == visitingNode.sourceLocation {
                return false
            }
        }
        visited.append(visitingNode)
        return true
    }
    
    func visit(_ decl : TopLevelDeclaration) throws -> Bool {
        SharedLogger.debug("Top Level Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug("\(decl.lexicalParent == nil)")
        SharedLogger.debug("Node already registered: \(!newVisit)")
        return true
    }
    
    func visit(_ decl: ClassDeclaration) throws -> Bool {
        SharedLogger.debug("Class Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug(decl.accessLevelModifier?.textDescription ?? "Internal")
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    func visit(_ decl: ConstantDeclaration) throws -> Bool {
        SharedLogger.debug("Constant Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug("Modifiers: " + decl.modifiers.textDescription)
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }
    
    // come back to this
    func visit(_ decl: DeinitializerDeclaration) throws -> Bool {
        SharedLogger.debug("Deinitialzer Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    func visit(_ decl: EnumDeclaration) throws -> Bool {
        SharedLogger.debug("Enum Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug(decl.accessLevelModifier?.textDescription ?? "Internal")
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    func visit(_ decl: ExtensionDeclaration) throws -> Bool {
        SharedLogger.debug("Extension Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug(decl.accessLevelModifier?.textDescription ?? "Internal")
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    func visit(_ decl: FunctionDeclaration) throws -> Bool {
        SharedLogger.debug("Function Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug("Name: " + decl.name.textDescription)
        SharedLogger.debug("GeneticParameter: " + (decl.genericParameterClause?.textDescription ?? ""))
        SharedLogger.debug("GeneticParameter: " + (decl.genericWhereClause?.textDescription ?? ""))
        SharedLogger.debug("Signature: " + decl.signature.textDescription)
        SharedLogger.debug("Modifiers: " + decl.modifiers.textDescription)
        SharedLogger.debug("Attributes: " + decl.attributes.textDescription)
        SharedLogger.debug("Source Location: \(decl.sourceLocation.description)" )
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    // include all
    func visit(_ decl: ImportDeclaration) throws -> Bool {
        SharedLogger.debug("Import Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug("Node already registered: \(!newVisit)")
        
      return true
    }
    
    // include all (for public and open)
    func visit(_ decl: InitializerDeclaration) throws -> Bool {
        SharedLogger.debug("Initializer Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    // dont include for now
    func visit(_ decl: OperatorDeclaration) throws -> Bool {
        SharedLogger.debug("Operator Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    // dont include for now
    func visit(_ decl: PrecedenceGroupDeclaration) throws -> Bool {
        SharedLogger.debug("PrecedenceGroup Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    
    func visit(_ decl: ProtocolDeclaration) throws -> Bool {
        SharedLogger.debug("Protocol Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug(decl.accessLevelModifier?.textDescription ?? "Internal")
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    func visit(_ decl: StructDeclaration) throws -> Bool {
        SharedLogger.debug("Struct Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug(decl.accessLevelModifier?.rawValue ?? "Internal")
        SharedLogger.debug("Source Location: \(decl.sourceLocation.description)")
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    func visit(_ decl: SubscriptDeclaration) throws -> Bool {
        SharedLogger.debug("Subscript Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug("Modifiers: " + decl.modifiers.textDescription)
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    func visit(_ decl: TypealiasDeclaration) throws -> Bool {
        SharedLogger.debug("Typealias Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug(decl.accessLevelModifier?.textDescription ?? "Internal")
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }

    func visit(_ decl: VariableDeclaration) throws -> Bool {
        SharedLogger.debug("Variable Declaration")
        let newVisit = registerVisit(to: decl)
        SharedLogger.debug(decl.attributes.textDescription)
        SharedLogger.debug("Modifiers: " + decl.modifiers.textDescription)
        SharedLogger.debug("Node already registered: \(!newVisit)")
      return true
    }
}


