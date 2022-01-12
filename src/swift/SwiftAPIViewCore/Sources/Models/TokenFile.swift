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

    /// Language string.
    let language = "Swift"

    /// List of APIVIew tokens to render
    var tokens: [TokenItem]

    /// List of APIView navigation items which display in the sidebar
    var navigation: [NavigationItem]

    /// Indentation level
    private var indentLevel = 0

    /// Number of spaces per indentation level
    private let indentSpaces = 4

    /// Tracks whether indentation is needed
    private var needsIndent = false

    /// Access modifier to expose via APIView
    let publicModifiers: [AccessLevelModifier] = [.public, .open]

    /// Tracks assigned definition IDs so they can be linked where key is the name and value is the definition ID (which could be different)
    private var definitionIds = [String: String]()

    var text: String {
        return tokens.map { $0.text }.joined()
    }

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

    func text(_ text: String, definitionId: String? = nil) {
        checkIndent()
        let item = TokenItem(definitionId: definitionId, navigateToId: nil, value: text, kind: .text)
        add(token: item)
    }

    func newLine() {
        // strip trailing whitespace token, except blank lines
        if tokens.last?.kind == .whitespace {
            let popped  = tokens.popLast()!
            // lines that consist only of whitespace must be preserved
            if tokens.last?.kind == .newline {
                add(token: popped)
            }
        }
        checkIndent()
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: nil, kind: .newline)
        add(token: item)
        lineIdMarker()
        needsIndent = true
    }

    func whitespace(count: Int = 1) {
        // don't double up on whitespace
        guard tokens.last?.kind != .whitespace else { return }
        let value = String(repeating: " ", count: count)
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .whitespace)
        add(token: item)
    }

    func punctuation(_ value: String) {
        checkIndent()
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .punctuation)
        add(token: item)
    }

    func keyword(value: String) {
        checkIndent()
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: value, kind: .keyword)
        add(token: item)
    }

    func lineIdMarker(definitionId: String? = nil) {
        checkIndent()
        let item = TokenItem(definitionId: definitionId, navigateToId: nil, value: nil, kind: .lineIdMarker)
        add(token: item)
    }

    func type(name: String, definitionId: String? = nil) {
        checkIndent()
        let item = TokenItem(definitionId: definitionId, navigateToId: definitionId, value: name, kind: .typeName)
        add(token: item)
    }

    func member(name: String, definitionId: String? = nil) {
        checkIndent()
        let item = TokenItem(definitionId: definitionId, navigateToId: nil, value: name, kind: .memberName)
        add(token: item)
    }

    func stringLiteral(_ text: String) {
        checkIndent()
        let item = TokenItem(definitionId: nil, navigateToId: nil, value: text, kind: .stringLiteral)
        add(token: item)
    }

    func indent(_ indentedCode: () -> Void) {
        indentLevel += 1
        indentedCode()
        indentLevel -= 1
    }

    func checkIndent() {
        guard needsIndent else { return }
        whitespace(count: indentLevel * indentSpaces)
        needsIndent = false
    }

    // MARK: Utility Methods

    /// Adds a token to the list
    internal func add(token: TokenItem) {
        tokens.append(token)
    }

    /// Constructs a definition ID and ensures it is unique.
    internal func defId(forName name: String, withPrefix prefix: String?) -> String {
        let defId = prefix != nil ? "\(prefix!).\(name)" : name
        guard !defId.contains(" ") else {
            fatalError("Definition ID should not contain whitespace")
        }
        if self.definitionIds[defId] != nil {
            // extension definition statements are permitted to repeat since
            // there is no way to make them unique
            if !(prefix ?? "").hasSuffix("extension") {
                fatalError("Definition ID should be unique")
            }
        }
        definitionIds[defId] = defId
        return defId
    }

    // MARK: Processing Methods

    /// This should be the only process method that APIViewManager should be able to call
    internal func process(statements: [Statement]) {
        let bundle = Bundle(for: Swift.type(of: self))
        let versionKey = "CFBundleShortVersionString"
        let apiViewVersion = bundle.object(forInfoDictionaryKey: versionKey) as? String ?? "Unknown"
        let defId = defId(forName: packageName, withPrefix: nil)
        text("Package parsed using Swift APIView (version \(apiViewVersion))")
        newLine()
        newLine()
        text("package")
        whitespace()
        text(packageName, definitionId: defId)
        whitespace()
        punctuation("{")
        newLine()
        indent {
            let stopIdx = statements.count - 1
            for (idx, statement) in statements.enumerated() {
                switch statement {
                case let decl as Declaration:
                    if process(decl, defIdPrefix: defId), idx != stopIdx {
                        // add an blank line for each declaration that is actually rendered
                        newLine()
                    }
                default:
                    SharedLogger.fail("Unsupported statement type: \(statement)")
                }
            }
        }
        punctuation("}")
        newLine()

        navigation = navigationTokens(from: statements)
        // ensure items appear in sorted order
        navigation.sort(by: { $0.text < $1.text })
        navigation.forEach { item in
            item.childItems.sort(by: { $0.text < $1.text })
        }
    }

    private func process(_ decl: ClassDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        guard publicModifiers.contains(accessLevel) else { return false }

        // register type as linkable
        let defId = defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)

        handle(attributes: decl.attributes)
        keyword(value: accessLevel.textDescription)
        whitespace()
        if decl.isFinal {
            keyword(value: "final")
            whitespace()
        }
        keyword(value: "class")
        whitespace()
        type(name: decl.name.textDescription, definitionId: defId)
        handle(clause: decl.genericParameterClause)
        handle(clause: decl.typeInheritanceClause)
        handle(clause: decl.genericWhereClause)
        whitespace()
        punctuation("{")
        newLine()
        indent {
            decl.members.forEach { member in
                handle(member: member, defId: defId)
            }
        }
        punctuation("}")
        newLine()
        return true
    }

    private func process(_ decl: StructDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        guard publicModifiers.contains(accessLevel) else { return false }

        // register type as linkable
        let defId = defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)

        handle(attributes: decl.attributes)
        keyword(value: accessLevel.textDescription)
        whitespace()
        keyword(value: "struct")
        whitespace()
        type(name: decl.name.textDescription, definitionId: defId)
        handle(clause: decl.genericParameterClause)
        handle(clause: decl.typeInheritanceClause)
        handle(clause: decl.genericWhereClause)
        whitespace()
        punctuation("{")
        newLine()
        indent {
            decl.members.forEach { member in
                handle(member: member, defId: defId)
            }
        }
        punctuation("}")
        newLine()
        return true
    }

    private func process(_ decl: EnumDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        guard publicModifiers.contains(accessLevel) else { return false }

        // register type as linkable
        let defId = defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)

        handle(attributes: decl.attributes)
        if decl.isIndirect {
            keyword(value: "indirect")
            whitespace()
        }
        keyword(value: accessLevel.textDescription)
        whitespace()
        keyword(value: "enum")
        whitespace()
        type(name: decl.name.textDescription, definitionId: defId)
        handle(clause: decl.genericParameterClause)
        handle(clause: decl.typeInheritanceClause)
        handle(clause: decl.genericWhereClause)
        whitespace()
        punctuation("{")
        newLine()
        indent {
            decl.members.forEach { member in
                handle(member: member, defId: defId)
            }
        }
        punctuation("}")
        newLine()
        return true
    }

    private func process(_ decl: ProtocolDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        guard publicModifiers.contains(accessLevel) else { return false }

        // register type as linkable
        let defId = defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)

        handle(attributes: decl.attributes)
        keyword(value: accessLevel.textDescription)
        whitespace()
        keyword(value: "protocol")
        whitespace()
        type(name: decl.name.textDescription, definitionId: defId)
        whitespace()
        handle(clause: decl.typeInheritanceClause)
        whitespace()
        punctuation("{")
        newLine()
        indent {
            decl.members.forEach { member in
                handle(member: member, defId: defId, overridingAccess: accessLevel)
            }
        }
        punctuation("}")
        newLine()
        return true
    }

    private func process(_ decl: TypealiasDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess ?? .internal
        guard publicModifiers.contains(accessLevel) else { return false }

        // register type as linkable
        let defId = defId(forName: decl.name.textDescription, withPrefix: defIdPrefix)

        handle(attributes: decl.attributes)
        keyword(value: accessLevel.textDescription)
        whitespace()
        keyword(value: "typealias")
        whitespace()
        type(name: decl.name.textDescription, definitionId: defId)
        handle(clause: decl.generic)
        whitespace()
        punctuation("=")
        whitespace()
        text(decl.assignment.textDescription)
        newLine()
        return true
    }

    private func process(_ decl: ExtensionDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        let accessLevel = decl.accessLevelModifier ?? overridingAccess

        // a missing access modifier for extensions does *not* automatically
        // imply internal access
        if let access = accessLevel {
            guard publicModifiers.contains(access) else {
                return false
            }
        }

        handle(attributes: decl.attributes)
        if let access = accessLevel {
            let value = access.textDescription
            keyword(value: value)
            whitespace()
        }
        keyword(value: "extension")
        whitespace()
        let defId = defId(forName: decl.type.textDescription, withPrefix: "\(defIdPrefix).extension")

        type(name: decl.type.textDescription, definitionId: defId)
        whitespace()
        handle(clause: decl.typeInheritanceClause)
        handle(clause: decl.genericWhereClause)
        punctuation("{")
        newLine()
        indent {
            decl.members.forEach { member in
                handle(member: member, defId: defId, overridingAccess: accessLevel)
            }
        }
        punctuation("}")
        newLine()
        return true
    }

    private func process(_ decl: ConstantDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        for item in decl.initializerList {
            if let typeModel = item.typeModel {
                let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
                return processMember(name: item.name, defId: defIdPrefix, attributes: decl.attributes, modifiers: decl.modifiers, typeModel: typeModel, isConst: true, defaultValue: item.defaultValue, accessLevel: accessLevel)
            }
        }
        SharedLogger.fail("Type information not found.")
    }

    private func process(_ decl: VariableDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        var name: String
        var typeModel: TypeModel

        switch decl.body {
        case let .initializerList(initializerList):
            for item in initializerList {
                let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
                if let typeModelVal = item.typeModel {
                    name = item.name
                    typeModel = typeModelVal
                    let defId = defId(forName: name, withPrefix: defIdPrefix)
                    return processMember(name: name, defId: defId, attributes: decl.attributes, modifiers: decl.modifiers, typeModel: typeModel, isConst: false, defaultValue: item.defaultValue, accessLevel: accessLevel)
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
        let defId = defId(forName: name, withPrefix: defIdPrefix)
        return processMember(name: name, defId: defId, attributes: decl.attributes, modifiers: decl.modifiers, typeModel: typeModel, isConst: false, defaultValue: nil, accessLevel: accessLevel)
    }

    private func process(_ decl: InitializerDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
        let defId = defId(forName: decl.fullName, withPrefix: defIdPrefix)
        return processInitializer(defId: defId, attributes: decl.attributes, modifiers: decl.modifiers, kind: decl.kind.textDescription, accessLevel: accessLevel,  genericParam: decl.genericParameterClause, throwsKind: decl.throwsKind, parameterList: decl.parameterList, genericWhere: decl.genericWhereClause)
    }

    private func process(_ decl: FunctionDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
        let defId = defId(forName: decl.fullName, withPrefix: defIdPrefix)

        let name = decl.name
        return processFunction(name: name.textDescription, defId: defId, attributes: decl.attributes, modifiers: decl.modifiers, accessLevel: accessLevel, signature: decl.signature, genericParam: decl.genericParameterClause, genericWhere: decl.genericWhereClause)
    }

    private func process(_ decl: SubscriptDeclaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        let accessLevel = decl.modifiers.accessLevel ?? overridingAccess ?? .internal
        let defId = defId(forName: "subscript", withPrefix: defIdPrefix)
        return processSubscript(defId: defId, attributes: decl.attributes, modifiers: decl.modifiers, accessLevel: accessLevel, genericParam: decl.genericParameterClause, parameterList: decl.parameterList, resultType: decl.resultType, genericWhere: decl.genericWhereClause)
    }

    private func process(_ decl: OperatorDeclaration, defIdPrefix: String) -> Bool {
        var kword: String
        var name: String? = nil
        var opName: String
        switch decl.kind {
        case let .infix(op, ident):
            kword = "infix"
            opName = op
            name = ident?.textDescription
        case let .prefix(op):
            kword = "prefix"
            opName = op
        case let .postfix(op):
            kword = "postfix"
            opName = op
        }
        keyword(value: kword)
        whitespace()
        keyword(value: "operator")
        whitespace()
        text(opName)
        if let name = name {
            punctuation(":")
            whitespace()

            // register type name to make linkable
            let defId = defId(forName: name, withPrefix: defIdPrefix)
            type(name: name, definitionId: defId)
        }
        newLine()
        return true
    }

    private func process(_ decl: PrecedenceGroupDeclaration, defIdPrefix: String) -> Bool {
        let name = decl.name.textDescription
        keyword(value: "precedencegroup")
        whitespace()
        type(name: name)
        whitespace()
        punctuation("{")
        newLine()
        indent {
            decl.attributes.forEach { attr in
                switch attr {
                case let .assignment(val):
                    keyword(value: "assignment")
                    punctuation(":")
                    whitespace()
                    keyword(value: String(val))
                case .associativityLeft:
                    keyword(value: "associativity")
                    punctuation(":")
                    whitespace()
                    keyword(value: "left")
                case .associativityNone:
                    keyword(value: "associativity")
                    punctuation(":")
                    whitespace()
                    keyword(value: "none")
                case .associativityRight:
                    keyword(value: "associativity")
                    punctuation(":")
                    whitespace()
                    keyword(value: "right")
                case let .higherThan(val):
                    keyword(value: "higherThan")
                    punctuation(":")
                    whitespace()
                    type(name: val.map { $0.textDescription }.joined(separator: "."))
                case  let .lowerThan(val):
                    keyword(value: "lowerThan")
                    punctuation(":")
                    whitespace()
                    type(name: val.map { $0.textDescription }.joined(separator: "."))
                }
                newLine()
            }
        }
        punctuation("}")
        newLine()
        return true
    }

    /// Returns false if declaration is skipped. True if it is processed.
    private func process(_ decl: Declaration, defIdPrefix: String, overridingAccess: AccessLevelModifier? = nil) -> Bool {
        switch decl {
        case let decl as ClassDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case let decl as ConstantDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case let decl as EnumDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case let decl as ExtensionDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case let decl as FunctionDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case let decl as InitializerDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case let decl as ProtocolDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case let decl as StructDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case let decl as TypealiasDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case let decl as VariableDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case _ as ImportDeclaration:
            // Imports are no-op
            return false
        case _ as DeinitializerDeclaration:
            // Deinitializers are never public
            return false
        case let decl as SubscriptDeclaration:
            return process(decl, defIdPrefix: defIdPrefix, overridingAccess: overridingAccess)
        case let decl as PrecedenceGroupDeclaration:
            // precedence groups are always public
            return process(decl, defIdPrefix: defIdPrefix)
        case let decl as OperatorDeclaration:
            // operators are always public
            return process(decl, defIdPrefix: defIdPrefix)
        default:
            SharedLogger.fail("Unsupported declaration: \(decl)")
        }
    }

    private func handle(async: AsyncKind) {
        switch `async` {
        case .async:
            keyword(value: `async`.textDescription)
            whitespace()
        default:
            return
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
        let name = parameter.externalName?.textDescription ?? parameter.localName.textDescription
        let typeModel = TypeModel(from: parameter.typeAnnotation)
        member(name: name)
        punctuation(":")
        whitespace()
        handle(typeModel: typeModel)
        if let defaultArgument = parameter.defaultArgumentClause {
            whitespace()
            punctuation("=")
            whitespace()
            stringLiteral(defaultArgument.textDescription)
        }
        if parameter.isVarargs {
            text("...")
        }
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
        if !signature.parameterList.isEmpty {
            let stopIdx = signature.parameterList.count - 1
            for (idx, parameter) in signature.parameterList.enumerated() {
                handle(parameter: parameter)
                if idx != stopIdx {
                    punctuation(",")
                    whitespace()
                }
            }
        }
        punctuation(")")
        whitespace()
        handle(async: signature.asyncKind)
        handle(throws: signature.throwsKind)
        handle(result: signature.result?.type)
        whitespace()
    }

    private func handle(clause typeInheritance: TypeInheritanceClause?) {
        guard let typeInheritance = typeInheritance else { return }
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

    private func handle(attributes: Attributes, inline: Bool = false) {
        guard !attributes.isEmpty else { return }
        // extra newline for readability
        inline ? whitespace() : newLine()
        attributes.forEach { attribute in
            keyword(value: "@\(attribute.name.textDescription)")
            if let argument = attribute.argumentClause {
                text(argument.textDescription)
            }
            inline ? whitespace() : newLine()
        }
    }

    private func handle(typeModel: TypeModel?) {
        guard let source = typeModel else { return }
        if let attributes = typeModel?.attributes {
            handle(attributes: attributes, inline: true)
        }
        if source.isArray { punctuation("[") }
        type(name: source.name)
        if source.isArray { punctuation("]") }
        if source.isOptional {
            punctuation("?")
        }
    }

    private func handle(tuple: TupleType?) {
        guard let tuple = tuple else { return }
        punctuation("(")
        let stopIdx = tuple.elements.count - 1
        for (idx, element) in tuple.elements.enumerated() {
            if let name = element.name?.textDescription {
                member(name: name)
                punctuation(":")
                whitespace()
            }
            handle(typeModel: TypeModel(from: element.type))
            if idx != stopIdx {
                punctuation(",")
                whitespace()
            }
        }
        punctuation(")")
    }

    private func handle(member: ClassDeclaration.Member, defId: String, overridingAccess: AccessLevelModifier? = nil) {
        switch member {
        case let .declaration(decl):
            _ = process(decl, defIdPrefix: defId, overridingAccess: overridingAccess)
        default:
            SharedLogger.fail("Unsupported member: \(member)")
        }
    }

    private func handle(member: StructDeclaration.Member, defId: String, overridingAccess: AccessLevelModifier? = nil) {
        switch member {
        case let .declaration(decl):
            _ = process(decl, defIdPrefix: defId, overridingAccess: overridingAccess)
        default:
            SharedLogger.fail("Unsupported member: \(member)")
        }
    }

    private func handle(member: EnumDeclaration.Member, defId: String, overridingAccess: AccessLevelModifier? = nil) {
        switch member {
        case let .declaration(decl):
            _ = process(decl, defIdPrefix: defId, overridingAccess: overridingAccess)
        case let .union(enumCase):
            enumCase.cases.forEach { enumCaseValue in
                let enumDefId = self.defId(forName: enumCaseValue.name.textDescription, withPrefix: defId)
                keyword(value: "case")
                whitespace()
                self.member(name: enumCaseValue.name.textDescription, definitionId: enumDefId)
                handle(tuple: enumCaseValue.tuple)
                newLine()
            }
        case let .rawValue(enumCase):
            enumCase.cases.forEach { enumCaseValue in
                let enumDefId = self.defId(forName: enumCaseValue.name.textDescription, withPrefix: defId)
                keyword(value: "case")
                whitespace()
                self.member(name: enumCaseValue.name.textDescription, definitionId: enumDefId)
                if let value = enumCaseValue.assignment {
                    whitespace()
                    punctuation("=")
                    whitespace()
                    switch value {
                    case let .boolean(val):
                        text(String(val))
                    case let .floatingPoint(val):
                        text(String(val))
                    case let .integer(val):
                        text(String(val))
                    case let .string(val):
                        text("\"\(val)\"")
                    }
                }
                newLine()
            }
        default:
            SharedLogger.fail("Unsupported member: \(member)")
        }
    }

    private func handle(member: ProtocolDeclaration.Member, defId: String, overridingAccess: AccessLevelModifier? = nil) {
        switch member {
        case let .associatedType(data):
            let name = data.name.textDescription
            keyword(value: "associatedtype")
            whitespace()
            self.member(name: name)
            if let inheritance = data.typeInheritance {
                handle(clause: inheritance)
            }
            newLine()
        case let .method(data):
            let accessLevel = data.modifiers.accessLevel ?? overridingAccess ?? .internal
            let defId = self.defId(forName: data.name.textDescription, withPrefix: defId)
            let name = data.name.textDescription
            _ = processFunction(name: name, defId: defId, attributes: data.attributes, modifiers: data.modifiers, accessLevel: accessLevel, signature: data.signature, genericParam: data.genericParameter, genericWhere: data.genericWhere)
        case let .property(data):
            let name = data.name.textDescription

            handle(attributes: data.attributes)
            handle(modifiers: data.modifiers)
            keyword(value: "var")
            whitespace()
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
            newLine()
        case let .initializer(data):
            let accessLevel = data.modifiers.accessLevel ?? overridingAccess ?? .internal
            let defId = self.defId(forName: data.fullName, withPrefix: defId)
            _ = processInitializer(defId: defId, attributes: data.attributes, modifiers: data.modifiers, kind: data.kind.textDescription, accessLevel: accessLevel,  genericParam: data.genericParameter, throwsKind: data.throwsKind, parameterList: data.parameterList, genericWhere: data.genericWhere)
        case let .subscript(data):
            let accessLevel = data.modifiers.accessLevel ?? overridingAccess ?? .internal
            let defId = self.defId(forName: "subscript", withPrefix: defId)
            _ = processSubscript(defId: defId, attributes: data.attributes, modifiers: data.modifiers, accessLevel: accessLevel, genericParam: data.genericParameter, parameterList: data.parameterList, resultType: data.resultType, genericWhere: data.genericWhere)
        default:
            SharedLogger.fail("Unsupported protocol member: \(member)")
        }
    }

    private func handle(member: ExtensionDeclaration.Member, defId: String, overridingAccess: AccessLevelModifier?) {
        switch member {
        case let .declaration(decl):
            _ = process(decl, defIdPrefix: defId, overridingAccess: overridingAccess)
        case let .compilerControl(statement):
            SharedLogger.fail("Unsupported statement: \(statement)")
        }
    }

    // MARK: Common Processors

    private func processMember(name: String, defId: String, attributes: Attributes, modifiers: DeclarationModifiers, typeModel: TypeModel, isConst: Bool, defaultValue: String?, accessLevel: AccessLevelModifier) -> Bool {
        guard publicModifiers.contains(accessLevel) else { return false }
        handle(attributes: attributes)
        handle(modifiers: modifiers)
        keyword(value: isConst ? "let" : "var")
        whitespace()
        member(name: name, definitionId: defId)
        punctuation(":")
        whitespace()
        handle(typeModel: typeModel)
        if let defaultValue = defaultValue {
            whitespace()
            punctuation("=")
            whitespace()
            text(defaultValue)
        }
        newLine()
        return true
    }

    private func processInitializer(defId: String, attributes: Attributes, modifiers: DeclarationModifiers, kind: String, accessLevel: AccessLevelModifier, genericParam: GenericParameterClause?, throwsKind: ThrowsKind, parameterList: [FunctionSignature.Parameter], genericWhere: GenericWhereClause?) -> Bool {
        guard publicModifiers.contains(accessLevel) else { return false }
        handle(attributes: attributes)
        handle(modifiers: modifiers)
        member(name: "init", definitionId: defId)
        punctuation(kind)
        handle(clause: genericParam)
        let initSignature = FunctionSignature(parameterList: parameterList, throwsKind: throwsKind, result: nil)
        handle(signature: initSignature)
        handle(clause: genericWhere)
        newLine()
        return true
    }

    private func processSubscript(defId: String, attributes: Attributes, modifiers: DeclarationModifiers, accessLevel: AccessLevelModifier, genericParam: GenericParameterClause?, parameterList: [FunctionSignature.Parameter], resultType: Type, genericWhere: GenericWhereClause?) -> Bool {
        guard publicModifiers.contains(accessLevel) else { return false }
        handle(attributes: attributes)
        handle(modifiers: modifiers)
        keyword(value: "subscript")
        handle(clause: genericParam)
        punctuation("(")
        parameterList.forEach { param in
            handle(parameter: param)
        }
        punctuation(")")
        whitespace()
        handle(result: resultType)
        handle(clause: genericWhere)
        newLine()
        return true
    }

    private func processFunction(name: String, defId: String, attributes: Attributes, modifiers: DeclarationModifiers, accessLevel: AccessLevelModifier, signature: FunctionSignature, genericParam: GenericParameterClause?, genericWhere: GenericWhereClause?) -> Bool {
        guard publicModifiers.contains(accessLevel) else { return false }
        handle(attributes: attributes)
        handle(modifiers: modifiers)
        keyword(value: "func")
        whitespace()
        member(name: name, definitionId: defId)
        handle(clause: genericParam)
        handle(signature: signature)
        handle(clause: genericWhere)
        newLine()
        return true
    }
}
