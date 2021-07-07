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
    func visit(_ decl: ClassDeclaration) throws -> Bool {
        switch decl.accessLevelModifier {
        case .open, .openSet, .public, .publicSet:
            return true
        default:
            return true
        }
      return true
    }

    func visit(_ decl: ConstantDeclaration) throws -> Bool {
        SharedLogger.debug("Constant Declaration")
        SharedLogger.debug(decl.modifiers.textDescription)
      return true
    }

    func visit(_ decl: DeinitializerDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: EnumDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: ExtensionDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: FunctionDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: ImportDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: InitializerDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: OperatorDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: PrecedenceGroupDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: ProtocolDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: StructDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: SubscriptDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: TypealiasDeclaration) throws -> Bool {
      return true
    }

    func visit(_ decl: VariableDeclaration) throws -> Bool {
      return true
    }
}


