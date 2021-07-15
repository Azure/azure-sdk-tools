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
    let publicModifiers: [AccessLevelModifier] = [.public, .open]

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

        navigationTokens(from: declarations)
        // ensure items appear in sorted order
        navigation.sort(by: { $0.text < $1.text })
        navigation.forEach { item in
            item.childItems.sort(by: { $0.text < $1.text })
        }
    }

    private func process(_ decl: TopLevelDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        if needsNewLine {
            newLine()
        }
        decl.statements.forEach { statement in
            switch statement {
            case let decl as Declaration:
                process(decl, overridingAccess: overridingAccess)
            default:
                SharedLogger.fail("Unsupported statement type: \(statement)")
            }
        }
    }

    private func process(_ decl: ClassDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        guard publicModifiers.contains(accessLevel) else {
            return
        }

        let defId = decl.name.textDescription
        definitionIds[defId] = defId

        newLine()
        handle(attributes: decl.attributes)
        lineIdMarker(definitionId: defId)
        keyword(value: accessLevel.textDescription)
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
            decl.members.forEach { member in
                handle(member: member)
            }
        }
        if needsNewLine {
            newLine()
        }
        punctuation("}")
    }

    private func process(_ decl: StructDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        guard publicModifiers.contains(accessLevel) else {
            return
        }
        let defId = decl.name.textDescription
        definitionIds[defId] = defId

        newLine()
        handle(attributes: decl.attributes)
        lineIdMarker(definitionId: defId)
        keyword(value: accessLevel.textDescription)
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
            decl.members.forEach { member in
                handle(member: member)
            }
        }
        if needsNewLine {
            newLine()
        }
        punctuation("}")
    }

    private func process(_ decl: EnumDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        guard publicModifiers.contains(accessLevel) else {
            return
        }
        let defId = decl.name.textDescription
        definitionIds[defId] = defId

        newLine()
        handle(attributes: decl.attributes)
        if decl.isIndirect {
            keyword(value: "indirect")
            whitespace()
        }
        lineIdMarker(definitionId: defId)
        keyword(value: accessLevel.textDescription)
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
            decl.members.forEach { member in
                handle(member: member, withParentId: defId)
            }
        }
        if needsNewLine {
            newLine()
        }
        punctuation("}")
    }

    private func process(_ decl: ProtocolDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        guard publicModifiers.contains(accessLevel) else {
            return
        }
        newLine()
        handle(attributes: decl.attributes)
        let defId = decl.name.textDescription
        definitionIds[defId] = defId
        keyword(value: accessLevel.textDescription)
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
                handle(member: member, withParentId: defId, overridingAccess: accessLevel)
            }
        }
        if needsNewLine {
            newLine()
        }
        punctuation("}")
    }

    private func process(_ decl: TypealiasDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        guard publicModifiers.contains(accessLevel) else {
            return
        }
        let defId = decl.name.textDescription
        definitionIds[defId] = defId

        newLine()
        handle(attributes: decl.attributes)
        keyword(value: accessLevel.textDescription)
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

    private func process(_ decl: ExtensionDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess

        // a missing access modifier for extensions does *not* automatically
        // imply internal access
        if let access = accessLevel {
            guard publicModifiers.contains(access) else {
                return
            }
        }
        newLine()
        handle(attributes: decl.attributes)
        if let access = accessLevel {
            let value = access.textDescription
            keyword(value: value)
            whitespace()
        }
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
                handle(member: member, overridingAccess: accessLevel)
            }
        }
        if needsNewLine {
            newLine()
        }
        punctuation("}")
    }

    private func process(_ decl: ConstantDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        for item in decl.initializerList {
            if let typeModelVal = item.typeModel {
                let name = item.name
                let typeModel = typeModelVal
                let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
                processMember(name: name, attributes: decl.attributes, modifiers: decl.modifiers, typeModel: typeModel, isConst: true, accessLevel: accessLevel)
                return
            }
        }
        SharedLogger.fail("Type information not found.")
    }

    private func process(_ decl: VariableDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        var name: String
        var typeModel: TypeModel

        switch decl.body {
        case let .initializerList(initializerList):
            for item in initializerList {
                let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
                if let typeModelVal = item.typeModel {
                    name = item.name
                    typeModel = typeModelVal
                    processMember(name: name, attributes: decl.attributes, modifiers: decl.modifiers, typeModel: typeModel, isConst: false, accessLevel: accessLevel)
                    return
                }
            }
            SharedLogger.fail("Type information not found.")
        case let .codeBlock(ident, typeAnno, _):
            typeModel = TypeModel(from: typeAnno)
            name = ident.textDescription
        case let .getterSetterKeywordBlock(ident, typeAnno, _):
            typeModel = TypeModel(from: typeAnno)
            name = ident.textDescription
        case let .willSetDidSetBlock(ident, typeAnno, expression, _):
            // TODO: complete implementation
            typeModel = TypeModel(from: typeAnno!)
            name = ident.textDescription
        default:
            SharedLogger.fail("Unsupported variable body type: \(decl.body)")
        }
        let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
        processMember(name: name, attributes: decl.attributes, modifiers: decl.modifiers, typeModel: typeModel, isConst: false, accessLevel: accessLevel)
    }

    private func process(_ decl: InitializerDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
        let defId = decl.textDescription
        processInitializer(defId: defId, attributes: decl.attributes, modifiers: decl.modifiers, kind: decl.kind.textDescription, accessLevel: accessLevel,  genericParam: decl.genericParameterClause, throwsKind: decl.throwsKind, parameterList: decl.parameterList, genericWhere: decl.genericWhereClause)
    }

    private func process(_ decl: FunctionDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
        let defId = decl.textDescription
        let name = decl.name.textDescription
        processFunction(name: name, defId: defId, attributes: decl.attributes, modifiers: decl.modifiers, accessLevel: accessLevel, signature: decl.signature, genericParam: decl.genericParameterClause, genericWhere: decl.genericWhereClause)
    }

    private func process(_ decl: SubscriptDeclaration, overridingAccess: AccessLevelModifier? = nil) {
        let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
        let defId = decl.textDescription
        processSubscript(defId: defId, attributes: decl.attributes, modifiers: decl.modifiers, accessLevel: accessLevel, genericParam: decl.genericParameterClause, parameterList: decl.parameterList, resultType: decl.resultType, genericWhere: decl.genericWhereClause)
    }

    private func process(_ decl: Declaration, overridingAccess: AccessLevelModifier? = nil) {
        switch decl {
        case let decl as ClassDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        case let decl as ConstantDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        case let decl as EnumDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        case let decl as ExtensionDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        case let decl as FunctionDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        case let decl as InitializerDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        case let decl as ProtocolDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        case let decl as StructDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        case let decl as TypealiasDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        case let decl as VariableDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        case _ as ImportDeclaration:
            // Imports are no-op
            return
        case _ as DeinitializerDeclaration:
            // Deinitializers are never public
            return
        case let decl as SubscriptDeclaration:
            return process(decl, overridingAccess: overridingAccess)
        default:
            SharedLogger.fail("Unsupported declaration: \(decl)")
        }
    }

    private func handle(throws: ThrowsKind) {
        switch `throws` {
        case .throwing, .rethrowing:
            keyword(value: `throws`.textDescription)
            whitespace()
        default:
            return
        }
    }

    private func handle(result: Type?) {
        guard let result = result else { return }
        let typeModel = TypeModel(from: result)
        punctuation("->")
        whitespace()
        handle(typeModel: typeModel)
    }

    private func handle(parameter: FunctionSignature.Parameter) {
        // TODO: eliminate reliance on textDescription
        let externalNameText = parameter.externalName.map { [$0.textDescription] } ?? []
        let localNameText = parameter.localName.textDescription.isEmpty ? [] : [parameter.localName.textDescription]
        let nameText = (externalNameText + localNameText).joined(separator: " ")
        let typeModel = TypeModel(from: parameter.typeAnnotation)
        let defaultText =
            parameter.defaultArgumentClause.map { " = \($0.textDescription)" } ?? ""
        let varargsText = parameter.isVarargs ? "..." : ""
        member(name: nameText)
        punctuation(":")
        whitespace()
        handle(typeModel: typeModel)
        stringLiteral(defaultText)
        text(varargsText)
    }

    private func handle(modifiers: DeclarationModifiers) {
        guard !modifiers.isEmpty else { return }
        modifiers.forEach { modifier in
            keyword(value: modifier.textDescription)
            whitespace()
        }
    }

    private func handle(signature: FunctionSignature) {
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
        handle(result: signature.result?.type)
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

    private func handle(clause genericParam: GenericParameterClause?) {
        guard let genericParam = genericParam else { return }
        // TODO: make dotted names linkable
        punctuation("<")
        let stopIdx = genericParam.parameterList.count - 1
        for (idx, param) in genericParam.parameterList.enumerated() {
            switch param {
            case let .identifier(type1):
                type(name: type1.textDescription)
            case let .protocolConformance(type1, protocol2):
                type(name: type1.textDescription)
                punctuation(":")
                whitespace()
                type(name: protocol2.textDescription)
            case let .typeConformance(type1, type2):
                type(name: type1.textDescription)
                punctuation(":")
                whitespace()
                type(name: type2.textDescription)
            }
            if idx != stopIdx {
                punctuation(",")
                whitespace()
            }
        }
        punctuation(">")
        whitespace()
    }

    private func handle(clause genericWhere: GenericWhereClause?) {
        guard let genericWhere = genericWhere else { return }
        whitespace()
        keyword(value: "where")
        whitespace()
        let stopIdx = genericWhere.requirementList.count - 1
        for (idx, requirement) in genericWhere.requirementList.enumerated() {
            // TODO: make dotted names linkable
            switch requirement {
            case let .protocolConformance(type1, protocol2):
                type(name: type1.textDescription)
                punctuation(":")
                whitespace()
                type(name: protocol2.textDescription)
            case let .sameType(type1, type2):
                type(name: type1.textDescription)
                whitespace()
                punctuation("==")
                whitespace()
                type(name: type2.textDescription)
            case let .typeConformance(type1, type2):
                type(name: type1.textDescription)
                punctuation(":")
                whitespace()
                type(name: type2.textDescription)
            }
            if idx != stopIdx {
                punctuation(",")
                whitespace()
            }
        }
        whitespace()
    }

    private func handle(attributes: Attributes) {
        guard !attributes.isEmpty else { return }
        attributes.forEach { attribute in
            keyword(value: "@\(attribute.name.textDescription)")
            if let argument = attribute.argumentClause {
                text(argument.textDescription)
            }
            newLine()
        }
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

    private func handle(member: ClassDeclaration.Member, overridingAccess: AccessLevelModifier? = nil) {
        switch member {
        case let .declaration(decl):
            process(decl, overridingAccess: overridingAccess)
        default:
            SharedLogger.fail("Unsupported member: \(member)")
        }
    }

    private func handle(member: StructDeclaration.Member, overridingAccess: AccessLevelModifier? = nil) {
        switch member {
        case let .declaration(decl):
            process(decl, overridingAccess: overridingAccess)
        default:
            SharedLogger.fail("Unsupported member: \(member)")
        }
    }

    private func handle(member: EnumDeclaration.Member, withParentId parentId: String, overridingAccess: AccessLevelModifier? = nil) {
        switch member {
        case let .declaration(decl):
            process(decl, overridingAccess: overridingAccess)
        case let .union(enumCase):
            enumCase.cases.forEach { enumCaseValue in
                newLine()
                let enumDefId = "\(parentId).\(enumCaseValue.name.textDescription)"
                lineIdMarker(definitionId: enumDefId)
                keyword(value: "case")
                whitespace()
                self.member(name: enumCaseValue.name.textDescription, definitionId: enumDefId)
            }
        case let .rawValue(enumCase):
            enumCase.cases.forEach { enumCaseValue in
                let enumDefId = "\(parentId).\(enumCaseValue.name.textDescription)"
                lineIdMarker(definitionId: enumDefId)
                keyword(value: "case")
                whitespace()
                newLine()
                self.member(name: enumCaseValue.name.textDescription, definitionId: enumDefId)
            }
        default:
            SharedLogger.fail("Unsupported member: \(member)")
        }
    }

    private func handle(member: ProtocolDeclaration.Member, withParentId parentId: String, overridingAccess: AccessLevelModifier? = nil) {
        switch member {
        case let .associatedType(data):
            let name = data.name.textDescription
            newLine()
            keyword(value: "associatedtype")
            whitespace()
            lineIdMarker(definitionId: "\(parentId).\(name)")
            self.member(name: name)
            if let inheritance = data.typeInheritance {
                handle(clause: inheritance)
            }
        case let .method(data):
            let accessLevel = data.modifiers.accessLevel ?? overridingAccess ?? .internal
            let defId = "\(parentId).\(data.textDescription)"
            let name = data.name.textDescription
            processFunction(name: name, defId: defId, attributes: data.attributes, modifiers: data.modifiers, accessLevel: accessLevel, signature: data.signature, genericParam: data.genericParameter, genericWhere: data.genericWhere)
        case let .property(data):
            let name = data.name.textDescription
            newLine()
            handle(attributes: data.attributes)
            handle(modifiers: data.modifiers)
            keyword(value: "var")
            whitespace()
            lineIdMarker(definitionId: "\(parentId).\(name)")
            self.member(name: name)
            punctuation(":")
            whitespace()
            handle(typeModel: TypeModel(from: data.typeAnnotation))
            whitespace()
            punctuation("{")
            whitespace()
            if data.getterSetterKeywordBlock.getter.mutationModifier != nil {
                keyword(value: "mutating")
                whitespace()
            }
            keyword(value: "get" )
            whitespace()
            if let setter = data.getterSetterKeywordBlock.setter {
                if setter.mutationModifier != nil {
                    keyword(value: "mutating")
                    whitespace()
                }
                keyword(value: "set")
                whitespace()
            }
            punctuation("}")
        case let .initializer(data):
            let accessLevel = data.modifiers.accessLevel ?? overridingAccess ?? .internal
            let defId = "\(parentId).\(data.textDescription)"
            processInitializer(defId: defId, attributes: data.attributes, modifiers: data.modifiers, kind: data.kind.textDescription, accessLevel: accessLevel,  genericParam: data.genericParameter, throwsKind: data.throwsKind, parameterList: data.parameterList, genericWhere: data.genericWhere)
        case let .subscript(data):
            let accessLevel = data.modifiers.accessLevel ?? overridingAccess ?? .internal
            let defId = "\(parentId).\(data.textDescription)"
            processSubscript(defId: defId, attributes: data.attributes, modifiers: data.modifiers, accessLevel: accessLevel, genericParam: data.genericParameter, parameterList: data.parameterList, resultType: data.resultType, genericWhere: data.genericWhere)
        default:
            SharedLogger.fail("Unsupported protocol member: \(member)")
        }
    }

    private func handle(member: ExtensionDeclaration.Member, overridingAccess: AccessLevelModifier?) {
        switch member {
        case let .declaration(decl):
            process(decl, overridingAccess: overridingAccess)
        case let .compilerControl(statement):
            SharedLogger.fail("Unsupported statement: \(statement)")
        }
    }

    // MARK: Common Processors

    private func processMember(name: String, attributes: Attributes, modifiers: DeclarationModifiers, typeModel: TypeModel, isConst: Bool, accessLevel: AccessLevelModifier) {
        guard publicModifiers.contains(accessLevel) else { return }
        newLine()
        handle(attributes: attributes)
        handle(modifiers: modifiers)
        keyword(value: isConst ? "let" : "var")
        whitespace()
        lineIdMarker(definitionId: name)
        member(name: name)
        punctuation(":")
        whitespace()
        handle(typeModel: typeModel)
    }

    private func processInitializer(defId: String, attributes: Attributes, modifiers: DeclarationModifiers, kind: String, accessLevel: AccessLevelModifier, genericParam: GenericParameterClause?, throwsKind: ThrowsKind, parameterList: [FunctionSignature.Parameter], genericWhere: GenericWhereClause?) {
        guard publicModifiers.contains(accessLevel) else { return }
        newLine()
        handle(attributes: attributes)
        handle(modifiers: modifiers)
        keyword(value: "init")
        punctuation(kind)
        lineIdMarker(definitionId: defId)
        handle(clause: genericParam)
        let initSignature = FunctionSignature(parameterList: parameterList, throwsKind: throwsKind, result: nil)
        handle(signature: initSignature)
        handle(clause: genericWhere)
    }

    private func processSubscript(defId: String, attributes: Attributes, modifiers: DeclarationModifiers, accessLevel: AccessLevelModifier, genericParam: GenericParameterClause?, parameterList: [FunctionSignature.Parameter], resultType: Type, genericWhere: GenericWhereClause?) {
        guard publicModifiers.contains(accessLevel) else { return }
        newLine()
        handle(attributes: attributes)
        handle(modifiers: modifiers)
        keyword(value: "subscript")
        lineIdMarker(definitionId: defId)
        handle(clause: genericParam)
        punctuation("(")
        parameterList.forEach { param in
            handle(parameter: param)
        }
        punctuation(")")
        whitespace()
        handle(result: resultType)
        handle(clause: genericWhere)
    }

    private func processFunction(name: String, defId: String, attributes: Attributes, modifiers: DeclarationModifiers, accessLevel: AccessLevelModifier, signature: FunctionSignature, genericParam: GenericParameterClause?, genericWhere: GenericWhereClause?) {
        guard publicModifiers.contains(accessLevel) else { return }
        newLine()
        handle(attributes: attributes)
        handle(modifiers: modifiers)
        keyword(value: "func")
        lineIdMarker(definitionId: defId)
        whitespace()
        type(name: name, definitionId: defId)
        handle(clause: genericParam)
        handle(signature: signature)
        handle(clause: genericWhere)
    }
}
