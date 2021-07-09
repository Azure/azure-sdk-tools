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
   
    private var needsNewLine = false
    
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
        needsNewLine = true
    }

    func newLine() {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: nil, kind: .newline)
        tokens.append(item)
        if indentLevel > 0 {
            whitespace(spaces: indentLevel)
        }
        needsNewLine = false
    }

    func whitespace(spaces: Int = 1) {
        let value = String(repeating: " ", count: spaces)
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .whitespace)
        tokens.append(item)
    }

    func punctuation(_ value: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .punctuation)
        tokens.append(item)
        needsNewLine = true
    }

    func keyword(value: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .keyword)
        tokens.append(item)
        needsNewLine = true
    }

    func lineIdMarker() {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: nil, kind: .lineIdMarker)
        tokens.append(item)
    }

    func type(name: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: name, kind: .typeName)
        tokens.append(item)
        needsNewLine = true
    }

    func member(name: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: name, kind: .memberName)
        tokens.append(item)
        needsNewLine = true
    }

    func stringLiteral(_ text: String) {
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: text, kind: .stringLiteral)
        tokens.append(item)
        needsNewLine = true
    }
    
    func indent(_ indentedCode: () -> Void) {
        indentLevel += indentSpaces
        indentedCode()
        indentLevel -= indentSpaces
    }
    
    func generateTokenFile(_ declarations: [TopLevelDeclaration]) {
        text("package")
        whitespace()
        text(packageName)
        whitespace()
        punctuation("{")
        indent {
            declarations.forEach { topLevelDecl in
                process(topLevelDecl)
            }
        }
        if needsNewLine  {
            newLine()
        }
        punctuation("}")
        newLine()
        
        navigation(from: declarations)
    }
    
    func process(_ decl: TopLevelDeclaration) {
        if needsNewLine  {
            newLine()
        }
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

    func process(_ decl: ClassDeclaration)  {
        // Gather Information
        SharedLogger.debug(decl.textDescription)
        SharedLogger.debug(decl.attributes.textDescription)
        SharedLogger.debug(decl.genericWhereClause?.textDescription ?? "")
        
        
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
        let value = (decl.accessLevelModifier ?? .internal).textDescription
        if needsNewLine {
            newLine()
        }
        if !decl.attributes.isEmpty {
            handle(attributes: decl.attributes)
        }
        
        keyword(value: value)
        whitespace()
        if decl.isFinal {
            keyword(value: "final")
            whitespace()
        }
        keyword(value: "class")
        whitespace()
        type(name: decl.name.textDescription)
        if let genericParam = decl.genericParameterClause {
            handle(clause: genericParam)
        }
        if let inheritance = decl.typeInheritanceClause {
            handle(clause: inheritance)
        }
        if let genericWhere = decl.genericWhereClause {
            handle(clause: genericWhere)
        }
        punctuation("{")
        indent {
            for member in decl.members {
                // TODO: Add members
                switch member {
                case .declaration(let decl):
                    process(decl)
                default:
                    continue
                }
            }
        }
        if needsNewLine {
            newLine()
        }
        punctuation("}")
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
    
    func handle(clause typeInheritance: TypeInheritanceClause) {
        var inherits = typeInheritance.textDescription
        inherits.removeFirst()
        text(":")
        type(name: inherits)
        whitespace()
    }
    
    func handle(clause genericParam: GenericParameterClause) {
        text(genericParam.textDescription)
        whitespace()
    }
    
    func handle(clause genericWhere: GenericWhereClause) {
        text(genericWhere.textDescription)
        whitespace()
    }
    
    func handle(attributes : Attributes) {
        keyword(value: attributes.textDescription)
        newLine()
    }
    
    func navigation(from declarations: [TopLevelDeclaration]) {
        let tags = NavigationTags(typeKind: .assembly)
        var packageNavItem = NavigationItem(text: packageName, navigationId: nil, childItems: [], tags: tags)
        declarations.forEach { decl in
            let item = navigation(from: decl)
            packageNavItem.childItems += item
        }
        self.navigation = [packageNavItem]
    }
    
    func navigation(from decl: TopLevelDeclaration) -> [NavigationItem] {
        var navItems : [NavigationItem] = []
        decl.statements.forEach { stmt in
            if let item = navigation(from: stmt) {
                navItems.append(item)
            }
        }
        return navItems
    }
    
    func navigation(from decl: ClassDeclaration) -> NavigationItem? {
        let tags = NavigationTags(typeKind: .class)
        var navItem = NavigationItem(text: decl.name.textDescription, navigationId: nil, childItems: [], tags: tags)
        decl.members.forEach { member in
            switch member {
            case .declaration(var decl):
                if let item = navigation(from: decl) {
                    navItem.childItems.append(item)
                }
            case .compilerControl(_):
                return
            }
        }
        return navItem
    }
    
    func navigation(from decl: StructDeclaration) -> NavigationItem? {
        return nil
    }
    
    func navigation(from decl: EnumDeclaration) -> NavigationItem? {
        return nil
    }

    func navigation(from decl: ProtocolDeclaration) -> NavigationItem? {
        return nil
    }
    
    func navigation(from decl: ExtensionDeclaration) -> NavigationItem? {
        return nil
    }
    
    func navigation(from decl: Statement) -> NavigationItem? {
        switch decl {
        case let decl as ClassDeclaration:
            return navigation(from: decl)
        case let decl as EnumDeclaration:
            return navigation(from: decl)
        case let decl as ExtensionDeclaration:
            return navigation(from: decl)
        case let decl as ProtocolDeclaration:
            return navigation(from: decl)
        case let decl as StructDeclaration:
            return navigation(from: decl)
        default:
          return nil
        }
    }
}
