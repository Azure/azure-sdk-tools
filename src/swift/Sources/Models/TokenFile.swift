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
        let item = TokenItem(definitionId: definitionId, navigateToId: nil, value: name, kind: .typeName)
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
    }

    private func process(_ decl: TopLevelDeclaration) {
        if needsNewLine {
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

        if needsNewLine {
            newLine()
        }
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

        navigation(add: NavigationItem(text: defId, navigationId: defId, typeKind: .class))
    }

    private func process(_ decl: StructDeclaration) {
        SharedLogger.debug("Struct Declaration")
        SharedLogger.debug(decl.textDescription)
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return
        }
        let value = (decl.accessLevelModifier ?? .internal).textDescription
        let defId = decl.name.textDescription

        if needsNewLine {
            newLine()
        }
        if !decl.attributes.isEmpty {
            handle(attributes: decl.attributes)
        }

        lineIdMarker(definitionId: defId)
        keyword(value: value)
        whitespace()
        keyword(value: "struct")
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

        if needsNewLine {
            newLine()
        }
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

    private func process(_ decl: ProtocolDeclaration) {
        SharedLogger.debug("Protocol Declaration")
        SharedLogger.debug(decl.textDescription)
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
    }

    private func process(_: TypealiasDeclaration) {}

    private func process(_ decl: VariableDeclaration) {
        let accessLevel = decl.modifiers.accessLevel
        var name = "NAME"
        var typeName = "TYPE"
        var isOptional = false
        var isArray = false
        let isStatic = decl.modifiers.isStatic

        decl.modifiers.verifySupported()

        switch decl.body {
        case let .initializerList(initializerList):
            for item in initializerList {
                if case let identPattern as IdentifierPattern = item.pattern {

                    name = identPattern.identifier.textDescription

                    if let typeAnnotation = identPattern.typeAnnotation?.type {
                        if case let typeAnno as OptionalType = typeAnnotation {
                            isOptional = true
                            if case let identifier as TypeIdentifier = typeAnno.wrappedType {
                                typeName = identifier.names.first!.name.textDescription
                            } else if case let identifier as ArrayType = typeAnno.wrappedType {
                                isArray = true
                                typeName = identifier.elementType.textDescription
                            } else {
                                SharedLogger.fail("Unsupported identifier: \(typeAnno.wrappedType)")
                            }
                        } else {
                            if case let identifier as TypeIdentifier = typeAnnotation {
                                typeName = identifier.names.first!.name.textDescription
                            } else if case let identifier as ArrayType = typeAnnotation {
                                isArray = true
                                typeName = identifier.elementType.textDescription
                            } else {
                                SharedLogger.fail("Unsupported identifier: \(typeAnnotation)")
                            }
                        }
                    }
                }
            }
        case let .codeBlock(ident, typeAnno, _):
            typeName = typeAnno.textDescription
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
        if isArray { punctuation("[") }
        type(name: typeName)
        if isArray { punctuation("]") }
        if isOptional {
            punctuation("?")
        }
    }

    private func process(_: ExtensionDeclaration) {}

    private func process(_ decl: ConstantDeclaration) {
        let accessLevel = decl.modifiers.accessLevel
        var name = "NAME"
        var typeName = "TYPE"
        var isOptional = false
        var isArray = false
        let isStatic = decl.modifiers.isStatic

        decl.modifiers.verifySupported()

        for item in decl.initializerList {
            if case let identPattern as IdentifierPattern = item.pattern {

                name = identPattern.identifier.textDescription

                if let typeAnnotation = identPattern.typeAnnotation?.type {
                    if case let typeAnno as OptionalType = typeAnnotation {
                        isOptional = true
                        if case let identifier as TypeIdentifier = typeAnno.wrappedType {
                            typeName = identifier.names.first!.name.textDescription
                        } else if case let identifier as ArrayType = typeAnno.wrappedType {
                            isArray = true
                            typeName = identifier.elementType.textDescription
                        } else {
                            SharedLogger.fail("Unsupported identifier: \(typeAnno.wrappedType)")
                        }
                    } else {
                        if case let identifier as TypeIdentifier = typeAnnotation {
                            typeName = identifier.names.first!.name.textDescription
                        } else if case let identifier as ArrayType = typeAnnotation {
                            isArray = true
                            typeName = identifier.elementType.textDescription
                        } else {
                            SharedLogger.fail("Unsupported identifier: \(typeAnnotation)")
                        }
                    }
                }
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
        if isArray { punctuation("[") }
        type(name: typeName)
        if isArray { punctuation("]") }
        if isOptional {
            punctuation("?")
        }
    }

    private func process(_: InitializerDeclaration) {
        // TODO: Prioritize
    }

    private func process(_: DeinitializerDeclaration) {}

    private func process(_ decl: FunctionDeclaration) {
        let accessLevel = decl.modifiers.accessLevel
        let name = decl.name.textDescription
        let isStatic = decl.modifiers.isStatic
        let throwsKind = decl.signature.throwsKind.textDescription
        var returnType: String? = nil

        decl.modifiers.verifySupported()

        if let result = decl.signature.result {
            // TODO: Return type should not include optionality, but this is quick and easy for now
            returnType = result.type.textDescription
        }

        guard publicModifiers.contains(accessLevel ?? .internal) else { return }
        newLine()
        keyword(value: accessLevel!.textDescription)
        whitespace()
        if isStatic {
            keyword(value: "static")
            whitespace()
        }
        keyword(value: "func")
        whitespace()
        lineIdMarker(definitionId: name)
        member(name: name)
        punctuation("(")
        for (idx, param) in decl.signature.parameterList.enumerated() {
            if let externalName = param.externalName?.textDescription {
                text(externalName)
                whitespace()
            }
            text(param.localName.textDescription)
            punctuation(":")
            whitespace()
            // TODO: More granularity here to extract the full type
            let paramType = String(param.typeAnnotation.textDescription.dropFirst(2))
            type(name: paramType)
            if let defaultClause = param.defaultArgumentClause {
                whitespace()
                punctuation("=")
                // TODO: Need to improve the granularity here
                text(defaultClause.textDescription)
            }
            if param.isVarargs {
                SharedLogger.fail("Varargs parameter not currently supported.")
            }
            if idx != decl.signature.parameterList.count - 1 {
                punctuation(",")
                whitespace()
            }
        }
        punctuation(")")
        whitespace()
        if throwsKind != ThrowsKind.nothrowing.textDescription {
            keyword(value: throwsKind)
            whitespace()
        }
        if let rtype = returnType {
            text("->")
            whitespace()
            // TODO: Link with definition ID
            type(name: rtype)
        }
    }

    private func process(_: ImportDeclaration) {}

    private func process(_ decl: Declaration) {
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

    private enum Members {
        case protocolDeclaration(members: [ProtocolDeclaration.Member])
        case structDeclaration(members: [StructDeclaration.Member])
        case classDeclaration(members: [ClassDeclaration.Member])
        case enumDeclaration(members: [EnumDeclaration.Member])
    }

    private func process(members _: Members) {
        return
    }

    private func handle(clause typeInheritance: TypeInheritanceClause) {
        var inherits = typeInheritance.textDescription
        inherits.removeFirst()
        text(":")
        type(name: inherits)
        whitespace()
    }

    private func handle(clause genericParam: GenericParameterClause) {
        text(genericParam.textDescription)
        whitespace()
    }

    private func handle(clause genericWhere: GenericWhereClause) {
        text(genericWhere.textDescription)
        whitespace()
    }

    private func handle(attributes: Attributes) {
        keyword(value: attributes.textDescription)
        newLine()
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
        let navItem = NavigationItem(text: decl.name.textDescription, navigationId: nil, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case var .declaration(decl):
                if let item = navigation(from: decl) {
                    navItem.childItems.append(item)
                }
            case .compilerControl:
                return
            }
        }
        return navItem
    }

    private func navigation(from _: StructDeclaration) -> NavigationItem? {
        return nil
    }

    private func navigation(from _: EnumDeclaration) -> NavigationItem? {
        return nil
    }

    private func navigation(from _: ProtocolDeclaration) -> NavigationItem? {
        return nil
    }

    private func navigation(from _: ExtensionDeclaration) -> NavigationItem? {
        return nil
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

extension DeclarationModifiers {

    func verifySupported() {
        for modifier in self {
            switch modifier {
            case .accessLevel, .static:
                continue
            default:
                SharedLogger.fail("Unsupported modifier: \(modifier)")
            }
        }
    }

    var accessLevel: AccessLevelModifier? {
        for modifier in self {
            switch modifier {
            case let .accessLevel(value):
                return value
            default:
                continue
            }
        }
        return nil
    }

    var isStatic: Bool {
        for modifier in self {
            switch modifier {
            case let .static:
                return true
            default:
                continue
            }
        }
        return false
    }
}
