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
import AST
import Foundation

/// The token file that is readable by APIView
class TokenFile: Codable {
    /// The name to be used in the APIView review list
    var name: String
    /// List of APIVIew tokens to render
    var tokens: [TokenItem]
    /// List of APIView navigation items which display in the sidebar
    var navigation: [NavigationItem]
    
    var language : String
    
    var packageName : String
    
    /// Indentation level
    private var indentLevel: Int = 0
    
    /// Indent Spaces
    private let indentSpaces = 4
    
    /// Access modifiers to expose to APIView
    private let publicModifiers: [AccessLevelModifier] = [.public, .open]
    
    // MARK: Initializers
    
    init(name: String) {
        self.name = name
        self.tokens = []
        self.navigation = []
        self.indentLevel = 0
        self.language = "Json"
        self.packageName = name
    }

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case name = "Name"
        case tokens = "Tokens"
        case language = "Language"
        case packageName = "PackageName"
        case navigation = "Navigation"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(name, forKey: .name)
        try container.encode(packageName, forKey: .packageName)
        try container.encode(language, forKey: .language)
        try container.encode(tokens, forKey: .tokens)
        try container.encode(navigation, forKey: .navigation)
    }

    // MARK: Methods

    func text(_ text: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: text, kind: .text)
        tokens.append(item)
    }

    func newline() {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: nil, kind: .newline)
        tokens.append(item)
        if indentLevel > 0 {
            whitespace(spaces: indentLevel)
        }
    }

    func whitespace(spaces: Int = 1) {
        let value = String(repeating: " ", count: spaces)
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .whitespace)
        tokens.append(item)
    }

    func punctuation(_ value: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .punctuation)
        tokens.append(item)
    }

    func keyword(value: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .keyword)
        tokens.append(item)
    }

    func lineIdMarker() {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: nil, kind: .lineIdMarker)
        tokens.append(item)
    }

    func type(name: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: name, kind: .typeName)
        tokens.append(item)
    }

    func member(name: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: name, kind: .memberName)
        tokens.append(item)
    }

    func stringLiteral(_ text: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: text, kind: .stringLiteral)
        tokens.append(item)
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
        keyword(value: value)
        whitespace()
        if decl.isFinal {
            keyword(value: "final")
            whitespace()
        }
        type(name: decl.name.textDescription)
        if let inheritance = decl.typeInheritanceClause {
            process(inheritance)
        }
        punctuation("{")
        indentLevel += indentSpaces
        newline()
        for member in decl.members {
            // TODO: Add members
            switch member {
            case .declaration(let decl):
                process(decl)
            default:
                continue
            }
        }
        indentLevel -= indentSpaces
        newline()
        punctuation("}")
        newline()
    }

    func process(_ decl: StructDeclaration) {
        
    }

    func process(_ decl: EnumDeclaration) {
    
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
        punctuation(":")
        whitespace()
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

