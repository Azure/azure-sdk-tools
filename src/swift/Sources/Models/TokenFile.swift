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

    /// Package name for the review
    var packageName: String

    /// The version string
    var versionString: String

    // TODO: Should be switched to Swift once APIView supports
    /// Language string.
    let language = "Json"

    /// List of APIVIew tokens to render
    var tokens: [TokenItem]

    /// List of APIView navigation items which display in the sidebar
    var navigation: [NavigationItem]

    /// Indentation level
    private var indentLevel = 0

    /// Number of spaces per indentation level
    private let indentSpaces = 4

    /// Access modifier to expose via APIView
    private let publicModifiers: [AccessLevelModifier] = [.public, .open]

    /// Controls whether a newline is needed
    private var needsNewLine = false

    /// Tracks assigned definition IDs so they can be linked where key is the name and value is the definition ID (which could be different)
    private var definitionIds = [String: String]()

    // MARK: Initializers

    init(name: String, packageName: String, versionString: String) {
        self.name = name
        tokens = []
        navigation = []
        self.packageName = packageName
        self.versionString = versionString
    }

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case name = "Name"
        case tokens = "Tokens"
        case language = "Language"
        case packageName = "PackageName"
        case navigation = "Navigation"
        case versionString = "VersionString"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(name, forKey: .name)
        try container.encode(packageName, forKey: .packageName)
        try container.encode(language, forKey: .language)
        try container.encode(tokens, forKey: .tokens)
        try container.encode(navigation, forKey: .navigation)
        try container.encode(versionString, forKey: .versionString)
    }

    // MARK: Token Emitter Methods

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

    func lineIdMarker(definitionId: String? = nil) {
        let item = TokenItem(definitionId: definitionId, navigateToId: nil, value: nil, kind: .lineIdMarker)
        tokens.append(item)
    }

    func type(name: String, definitionId: String? = nil) {
        let linkId = definitionIds[name]
        let item = TokenItem(definitionId: definitionId, navigateToId: linkId, value: name, kind: .typeName)
        tokens.append(item)
        needsNewLine = true
    }

    func member(name: String, definitionId: String? = nil) {
        let item = TokenItem(definitionId: definitionId, navigateToId: nil, value: name, kind: .memberName)
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

    // MARK: Processing Methods

    /// This should be the only process method that APIViewManager should be able to call
    internal func process(_ declarations: [TopLevelDeclaration]) {
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
        if needsNewLine {
            newLine()
        }
        punctuation("}")
        newLine()

        navigation(from: declarations)
        // ensure items appear in sorted order
        navigation.sort(by: {$0.text < $1.text })
        for item in navigation {
            item.childItems.sort(by: { $0.text < $1.text })
        }
    }

    private func process(_ decl: TopLevelDeclaration) {
        if needsNewLine {
            newLine()
        }
        for statement in decl.statements {
            switch statement {
            case let decl as Declaration:
                process(decl)
            default:
                continue
            }
        }
    }

    private func process(_ decl: ClassDeclaration) {
        // Gather Information
        SharedLogger.debug(decl.textDescription)
        SharedLogger.debug(decl.attributes.textDescription)
        SharedLogger.debug(decl.genericWhereClause?.textDescription ?? "")

        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
        let value = (decl.accessLevelModifier ?? .internal).textDescription
        let defId = decl.name.textDescription
        definitionIds[defId] = defId

        newLine()
        if !decl.attributes.isEmpty {
            handle(attributes: decl.attributes)
        }

        lineIdMarker(definitionId: defId)
        keyword(value: value)
        whitespace()
        if decl.isFinal {
            keyword(value: "final")
            whitespace()
        }
        keyword(value: "class")
        whitespace()
        type(name: decl.name.textDescription, definitionId: defId)
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

    private func process(_ decl: StructDeclaration) {
        SharedLogger.debug("Struct Declaration")
        SharedLogger.debug(decl.textDescription)
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
        let value = (decl.accessLevelModifier ?? .internal).textDescription
        let defId = decl.name.textDescription
        definitionIds[defId] = defId

        newLine()
        if !decl.attributes.isEmpty {
            handle(attributes: decl.attributes)
        }

        lineIdMarker(definitionId: defId)
        keyword(value: value)
        whitespace()
        keyword(value: "struct")
        whitespace()
        type(name: decl.name.textDescription, definitionId: defId)
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
                switch member {
                case let .declaration(decl):
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
    
    private func process(_ decl: EnumDeclaration) {
        SharedLogger.debug("Enum Declaration")
        SharedLogger.debug(decl.textDescription)
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
        let value = (decl.accessLevelModifier ?? .internal).textDescription
        let defId = decl.name.textDescription
        definitionIds[defId] = defId
        
        newLine()
        if !decl.attributes.isEmpty {
            handle(attributes: decl.attributes)
        }
        if decl.isIndirect {
            keyword(value: "indirect")
            whitespace()
        }
        lineIdMarker(definitionId: defId)
        keyword(value: value)
        whitespace()
        keyword(value: "enum")
        whitespace()
        type(name: decl.name.textDescription, definitionId: defId)
        if let genericParam = decl.genericParameterClause {
            whitespace()
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
                switch member {
                case .declaration(let decl):
                    process(decl)
                case .union(let enumCase):
                    enumCase.cases.forEach { enumCaseValue in
                        newLine()
                        let enumDefId = "\(decl.name.textDescription).\(enumCaseValue.name.textDescription)"
                        lineIdMarker(definitionId: enumDefId)
                        keyword(value: "case")
                        whitespace()
                        self.member(name: enumCaseValue.name.textDescription, definitionId: enumDefId)
                    }
                case .rawValue(let enumCase):
                    enumCase.cases.forEach { enumCaseValue in
                        let enumDefId = "\(decl.name.textDescription).\(enumCaseValue.name.textDescription)"
                        lineIdMarker(definitionId: enumDefId)
                        keyword(value: "case")
                        whitespace()
                        newLine()
                        self.member(name: enumCaseValue.name.textDescription, definitionId: enumDefId)
                    }
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

    private func process(_ decl: ProtocolDeclaration) {
        SharedLogger.debug("Protocol Declaration")
        SharedLogger.debug(decl.textDescription)
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
        let value = (decl.accessLevelModifier ?? .internal).textDescription
        newLine()
        if !decl.attributes.isEmpty {
            handle(attributes: decl.attributes)
        }
        let defId = decl.name.textDescription
        definitionIds[defId] = defId

        keyword(value: value)
        whitespace()
        keyword(value: "protocol")
        whitespace()
        type(name: decl.name.textDescription, definitionId: defId)
        whitespace()
        if let inheritance = decl.typeInheritanceClause {
            handle(clause: inheritance)
        }
        punctuation("{")
        indent {
            decl.members.forEach { member in
                // TODO: Need to complete this
            }
        }
        if needsNewLine {
            newLine()
        }
        punctuation("}")
        
    }

    private func process(_ decl : TypealiasDeclaration) {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
        let value = (decl.accessLevelModifier ?? .internal).textDescription
        let defId = decl.name.textDescription
        definitionIds[defId] = defId

        newLine()
        if !decl.attributes.isEmpty {
            handle(attributes: decl.attributes)
        }
        keyword(value: value)
        whitespace()
        keyword(value: "typealias")
        whitespace()
        type(name: decl.name.textDescription, definitionId: defId)
        if let genericParam = decl.generic {
            handle(clause: genericParam)
        }
        whitespace()
        punctuation("=")
        whitespace()
        text(decl.assignment.textDescription)
    }

    private func process(_ decl: VariableDeclaration) {
        let accessLevel = decl.modifiers.accessLevel
        var name = "NAME"
        let isStatic = decl.modifiers.isStatic
        var typeModel: TypeModel? = nil

        decl.modifiers.verifySupported()

        switch decl.body {
        case let .initializerList(initializerList):
            for item in initializerList {
                if case let identPattern as IdentifierPattern = item.pattern {
                    name = identPattern.identifier.textDescription
                    typeModel = TypeModel(from: identPattern)
                    break
                }
            }
        case let .codeBlock(ident, typeAnno, _):
            typeModel = TypeModel(from: typeAnno)
            name = ident.textDescription
        case let .getterSetterKeywordBlock(ident, typeAnno, _):
            typeModel = TypeModel(from: typeAnno)
            name = ident.textDescription
        default:
            SharedLogger.fail("Unsupported variable body type: \(decl.body)")
        }

        guard publicModifiers.contains(accessLevel ?? .internal) else { return }
        newLine()
        keyword(value: accessLevel!.textDescription)
        whitespace()
        if isStatic {
            keyword(value: "static")
            whitespace()
        }
        keyword(value: "var")
        whitespace()
        lineIdMarker(definitionId: name)
        member(name: name)
        punctuation(":")
        whitespace()
        handle(typeModel: typeModel)
    }
    
    private func process(_ decl: ExtensionDeclaration) {
        SharedLogger.debug("Extension Declaration")
        SharedLogger.debug(decl.textDescription)
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
        let value = (decl.accessLevelModifier ?? .internal).textDescription
        newLine()
        if !decl.attributes.isEmpty {
            handle(attributes: decl.attributes)
        }
        keyword(value: value)
        whitespace()
        keyword(value: "extension")
        whitespace()
        let defId = decl.type.textDescription
        lineIdMarker(definitionId: defId)
        type(name: decl.type.textDescription, definitionId: defId)
        whitespace()
        if let inheritance = decl.typeInheritanceClause {
            handle(clause: inheritance)
        }
        if let genericWhere = decl.genericWhereClause {
            handle(clause: genericWhere)
        }
        punctuation("{")
        indent {
            decl.members.forEach { member in
                switch member {
                case .declaration(let decl):
                    process(decl)
                default:
                    return
                }
            }
        }
        if needsNewLine {
            newLine()
        }
        punctuation("}")
    }

    private func process(_ decl: ConstantDeclaration) {
        let accessLevel = decl.modifiers.accessLevel
        var name = "NAME"
        let isStatic = decl.modifiers.isStatic
        var typeModel: TypeModel? = nil

        decl.modifiers.verifySupported()

        for item in decl.initializerList {
            if case let identPattern as IdentifierPattern = item.pattern {
                name = identPattern.identifier.textDescription
                typeModel = TypeModel(from: identPattern)
            }
        }

        guard publicModifiers.contains(accessLevel ?? .internal) else { return }
        newLine()
        keyword(value: accessLevel!.textDescription)
        whitespace()
        if isStatic {
            keyword(value: "static")
            whitespace()
        }
        keyword(value: "let")
        whitespace()
        lineIdMarker(definitionId: name)
        member(name: name)
        punctuation(":")
        whitespace()
        handle(typeModel: typeModel)
    }

    private func process(_ decl : InitializerDeclaration) {
        SharedLogger.debug("Initializer Declaration")
        guard publicModifiers.contains(decl.modifiers.accessLevel ?? .internal) else {
            return
        }
        newLine()
        if !decl.attributes.isEmpty {
            handle(attributes: decl.attributes)
        }
        if !decl.modifiers.isEmpty {
            handle(modifiers: decl.modifiers)
        }
        let defId = decl.textDescription
        keyword(value: "init")
        lineIdMarker(definitionId: defId)
        if let genericParam = decl.genericParameterClause {
            handle(clause: genericParam)
        }
        let initSignature = FunctionSignature(parameterList: decl.parameterList, throwsKind: decl.throwsKind, result: nil)
        handle(signature: initSignature)
        if let genericWhere = decl.genericWhereClause {
            handle(clause: genericWhere)
        }
    }

    private func process(_ decl: FunctionDeclaration) {
        SharedLogger.debug("Function Declaration")
        SharedLogger.debug(decl.signature.textDescription)

        guard publicModifiers.contains(decl.modifiers.accessLevel ?? .internal) else {
            return
        }
        newLine()
        if !decl.attributes.isEmpty {
            handle(attributes: decl.attributes)
        }
        handle(modifiers: decl.modifiers)
        keyword(value: "func")
        lineIdMarker(definitionId: name)
        whitespace()
        type(name: decl.name.textDescription, definitionId: decl.textDescription)
        if let genericParam = decl.genericParameterClause {
            whitespace()
            handle(clause: genericParam)
        }
        handle(signature: decl.signature)
    }

    private func process(_ decl: Declaration) {
        switch decl {
        case let decl as ClassDeclaration:
            return process(decl)
        case let decl as ConstantDeclaration:
            return process(decl)
        case let decl as EnumDeclaration:
            return process(decl)
        case let decl as ExtensionDeclaration:
            return process(decl)
        case let decl as FunctionDeclaration:
            return process(decl)
        case let decl as InitializerDeclaration:
            return process(decl)
        case let decl as ProtocolDeclaration:
            return process(decl)
        case let decl as StructDeclaration:
            return process(decl)
        case let decl as TypealiasDeclaration:
            return process(decl)
        case let decl as VariableDeclaration:
            return process(decl)
        case _ as ImportDeclaration:
            // Imports are no-op
            return
        default:
            SharedLogger.fail("Unsupported declaration: \(decl)")
        }
    }

    private func handle(modifiers: DeclarationModifiers) {
        keyword(value: modifiers.textDescription)
        whitespace()
    }
    
    private func handle(signature: FunctionSignature) {
        func handle(parameter: FunctionSignature.Parameter) {
            let externalNameText = parameter.externalName.map({ [$0.textDescription] }) ?? []
            let localNameText = parameter.localName.textDescription.isEmpty ? [] : [parameter.localName.textDescription]
            let nameText = (externalNameText + localNameText).joined(separator: " ")
            let typeModel = TypeModel(from: parameter.typeAnnotation)
            let defaultText =
                parameter.defaultArgumentClause.map({ " = \($0.textDescription)" }) ?? ""
            let varargsText = parameter.isVarargs ? "..." : ""
            member(name: nameText)
            punctuation(":")
            whitespace()
            self.handle(typeModel: typeModel)
            stringLiteral(defaultText)
            text(varargsText)
        }
        
        func handle(throws: ThrowsKind) {
            switch `throws` {
            case .throwing, .rethrowing:
                keyword(value: `throws`.textDescription)
                whitespace()
            default:
                return
            }
        }
        
        func handle(result: FunctionResult?) {
            guard let result = result else {
                return
            }
            let typeModel = TypeModel(from: result.type)
            punctuation("->")
            whitespace()
            self.handle(typeModel: typeModel)
        }

        punctuation("(")
        var count = signature.parameterList.count - 1
        signature.parameterList.forEach { parameter in
            handle(parameter: parameter)
            if count > 0 {
                punctuation(",")
                whitespace()
                count -= 1
            }
        }
        punctuation(")")
        whitespace()
        handle(throws: signature.throwsKind)
        handle(result: signature.result)
    }
    
    private func handle(clause typeInheritance: TypeInheritanceClause) {
        punctuation(":")
        whitespace()
        for (idx, item) in typeInheritance.typeInheritanceList.enumerated() {
            let typeModel = TypeModel(from: item)
            handle(typeModel: typeModel)
            if idx != typeInheritance.typeInheritanceList.count - 1 {
                punctuation(",")
                whitespace()
            }
        }
        whitespace()
    }

    private func handle(clause genericParam: GenericParameterClause) {
        // TODO: Remove reliance on textDescription
        text(genericParam.textDescription)
        whitespace()
    }

    private func handle(clause genericWhere: GenericWhereClause) {
        // TODO: Remove reliance on textDescription
        text(genericWhere.textDescription)
        whitespace()
    }

    private func handle(attributes: Attributes) {
        // TODO: remove reliance on textDescription
        keyword(value: attributes.textDescription)
        newLine()
    }

    private func handle(typeModel: TypeModel?) {
        guard let source = typeModel else { return }
        if source.isArray { punctuation("[") }
        type(name: source.name)
        if source.isArray { punctuation("]") }
        if source.isOptional {
            punctuation("?")
        }
    }

    // MARK: Navigation Emitter Methods

    private func navigation(add item: NavigationItem) {
        if let topItem = navigation.last {
            topItem.childItems.append(item)
        } else {
            navigation.append(item)
        }
    }

    private func navigation(from declarations: [TopLevelDeclaration]) {
        let packageNavItem = NavigationItem(text: packageName, navigationId: nil, typeKind: .assembly)
        declarations.forEach { decl in
            let item = navigation(from: decl)
            packageNavItem.childItems += item
        }
        navigation = [packageNavItem]
    }

    private func navigation(from decl: TopLevelDeclaration) -> [NavigationItem] {
        var navItems: [NavigationItem] = []
        decl.statements.forEach { stmt in
            if let item = navigation(from: stmt) {
                navItems.append(item)
            }
        }
        return navItems
    }

    private func navigation(from decl: ClassDeclaration) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(text: decl.name.textDescription, navigationId: decl.name.textDescription, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let item = navigation(from: decl) {
                    navItem.childItems.append(item)
                }
            case .compilerControl:
                return
            }
        }
        return navItem
    }

    private func navigation(from decl: StructDeclaration) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(text: decl.name.textDescription, navigationId: decl.name.textDescription, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let item = navigation(from: decl) {
                    navItem.childItems.append(item)
                }
            case .compilerControl:
                return
            }
        }
        return navItem
    }

    private func navigation(from decl: EnumDeclaration) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(text: decl.name.textDescription, navigationId: decl.name.textDescription, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let item = navigation(from: decl) {
                    navItem.childItems.append(item)
                }
            default:
                return
            }
        }
        return navItem
    }

    private func navigation(from decl: ProtocolDeclaration) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(text: decl.name.textDescription, navigationId: decl.name.textDescription, typeKind: .class)
        return navItem
    }

    private func navigation(from decl: ExtensionDeclaration) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(text: decl.type.textDescription, navigationId: decl.type.textDescription, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let item = navigation(from: decl) {
                    navItem.childItems.append(item)
                }
            default:
                return
            }
        }
        return navItem
    }

    private func navigation(from decl: Statement) -> NavigationItem? {
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
