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
    var tokenFile = TokenFile(name: "TestFile")

    let publicModifiers: [AccessLevelModifier] = [.public, .open]

    // MARK: Methods

    func run() throws {
        // TODO: Re-enable after testing
//        guard let sourcePath = args.source else {
//            SharedLogger.fail("usage error: SwiftAPIView --source PATH")
//        }
        let sourcePath = "/Users/travisprescott/repos/azure-sdk-for-ios/sdk/communication/AzureCommunicationChat/Source/ChatClient.swift"
        guard let sourceUrl = URL(string: args.source ?? sourcePath) else {
            SharedLogger.fail("usage error: `--source PATH` was invalid.")
        }

        try buildTokenFile(from: sourceUrl)

        let destUrl: URL
        if let destPath = args.dest {
            guard let dest = URL(string: destPath) else {
                SharedLogger.fail("usage error: `--dest PATH` was invalid.")
            }
            destUrl = dest
        } else {
            let destPath = "SwiftAPIView.json"
            guard let dest = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first?.appendingPathComponent(destPath) else {
                SharedLogger.fail("Could not access file system.")
            }
            destUrl = dest
        }
        do {
            let encoder = JSONEncoder()
            let tokenData = try encoder.encode(tokenFile)
            try tokenData.write(to: destUrl)
        } catch {
            SharedLogger.fail(error.localizedDescription)
        }
    }

    func buildTokenFile(from sourceUrl: URL) throws {
        SharedLogger.debug("URL: \(sourceUrl.absoluteString)")
        // TODO: This should loop through all source files instead of targeting one
        let sourceFile = try SourceReader.read(at: sourceUrl.absoluteString)
        let parser = Parser(source: sourceFile)
        let topLevelDecl = try parser.parse()
        process(topLevelDecl)
    }

    func process(_ decl: TopLevelDeclaration) {
        for statement in decl.statements {
            switch statement {
            case let decl as ClassDeclaration:
              process(decl)
            case let decl as ConstantDeclaration:
              process(decl)
            case let decl as DeinitializerDeclaration:
              process(decl)
            case let decl as EnumDeclaration:
              process(decl)
            case let decl as ExtensionDeclaration:
              process(decl)
            case let decl as FunctionDeclaration:
              process(decl)
            case let decl as ImportDeclaration:
              process(decl)
            case let decl as InitializerDeclaration:
              process(decl)
            case let decl as OperatorDeclaration:
              continue // process(decl)
            case let decl as PrecedenceGroupDeclaration:
              continue // process(decl)
            case let decl as ProtocolDeclaration:
              process(decl)
            case let decl as StructDeclaration:
              process(decl)
            case let decl as SubscriptDeclaration:
              continue // process(decl)
            case let decl as TypealiasDeclaration:
              process(decl)
            case let decl as VariableDeclaration:
              process(decl)
            default:
                continue
            }
        }
    }

    func process(_ decl: ClassDeclaration) {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
        let value = (decl.accessLevelModifier ?? .internal).textDescription
        tokenFile.addKeyword(value: value)
        tokenFile.addWhitespace()
        if decl.isFinal {
            tokenFile.addKeyword(value: "final")
            tokenFile.addWhitespace()
        }
        tokenFile.addType(name: decl.name.textDescription)
        if let inheritance = decl.typeInheritanceClause {
            process(inheritance)
        }
        tokenFile.addPunctuation("{")
        tokenFile.indentLevel += 2
        tokenFile.addNewline()
        for member in decl.members {
            // TODO: Add members
            switch member {
            case .declaration(let decl):
                process(decl)
            default:
                continue
            }
        }
        tokenFile.indentLevel -= 2
        tokenFile.addNewline()
        tokenFile.addPunctuation("}")
        tokenFile.addNewline()
    }

    func process(_ decl: StructDeclaration) {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
    }

    func process(_ decl: EnumDeclaration) {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
    }

    func process(_ decl: ProtocolDeclaration) {
        
    }
    
    func process(_ decl: TypealiasDeclaration) {
        
    }
    
    func process(_ decl: VariableDeclaration) {
        
    }
    
    func process(_ decl: ExtensionDeclaration) {
        
    }
    
    func process(_ decl: ConstantDeclaration) {
        
    }
    
    func process(_ decl: InitializerDeclaration) {
        
    }
    
    func process(_ decl: DeinitializerDeclaration) {
        
    }
    
    func process(_ decl: FunctionDeclaration) {
        
    }
    
    func process(_ decl: ImportDeclaration) {
        
    }
    

    
    func process(_ clause: TypeInheritanceClause) {
        tokenFile.addPunctuation(":")
        tokenFile.addWhitespace()
        for item in clause.typeInheritanceList {
            // TODO: Add type inheritance
            let test = "best"
        }
    }
    
    func process(_ decl: Declaration) {
        switch decl {
        case let decl as ClassDeclaration:
          return process(decl)
        case let decl as ConstantDeclaration:
          return process(decl)
        case let decl as DeinitializerDeclaration:
          return process(decl)
        case let decl as EnumDeclaration:
          return process(decl)
        case let decl as ExtensionDeclaration:
          return process(decl)
        case let decl as FunctionDeclaration:
          return process(decl)
        case let decl as ImportDeclaration:
          return process(decl)
        case let decl as InitializerDeclaration:
          return process(decl)
        case let decl as OperatorDeclaration:
          return // process(decl)
        case let decl as PrecedenceGroupDeclaration:
          return // process(decl)
        case let decl as ProtocolDeclaration:
          return process(decl)
        case let decl as StructDeclaration:
          return process(decl)
        case let decl as SubscriptDeclaration:
          return // process(decl)
        case let decl as TypealiasDeclaration:
          return process(decl)
        case let decl as VariableDeclaration:
          return process(decl)
        default:
          return // no implementation for this declaration, just continue
        }
    }
}
