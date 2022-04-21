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

extension Declaration {

    /// Convert a declaration to a Tokenizable instance.
    func toTokenizable(withParent parent: Linkable) -> Tokenizable? {
        switch self {
        case let self as ClassDeclaration:
            return ClassModel(from: self, parent: parent)
        case let self as ConstantDeclaration:
            return ConstantModel(from: self, parent: parent)
        case let self as EnumDeclaration:
            return EnumModel(from: self, parent: parent)
        case let self as ExtensionDeclaration:
            return ExtensionModel(from: self, parent: parent)
        case let self as FunctionDeclaration:
            return FunctionModel(from: self, parent: parent)
        case let self as InitializerDeclaration:
            return InitializerModel(from: self, parent: parent)
        case let self as ProtocolDeclaration:
            return ProtocolModel(from: self, parent: parent)
        case let self as StructDeclaration:
            return StructModel(from: self, parent: parent)
        case let self as TypealiasDeclaration:
            return TypealiasModel(from: self, parent: parent)
        case let self as VariableDeclaration:
            return VariableModel(from: self, parent: parent)
        case _ as ImportDeclaration:
            // Imports are no-op
            return nil
        case _ as DeinitializerDeclaration:
            // Deinitializers are never public
            return nil
        case let self as SubscriptDeclaration:
            return SubscriptModel(from: self, parent: parent)
        case let self as PrecedenceGroupDeclaration:
            // precedence groups are always public
            return PrecedenceGroupModel(from: self, parent: parent)
        case let self as OperatorDeclaration:
            // operators are always public
            return OperatorModel(from: self, parent: parent)
        default:
            SharedLogger.fail("Unsupported declaration: \(self)")
        }
    }

    /// Returns the access level, if found, for the declaration.
    var accessLevel: AccessLevelModifier? {
        switch self {
        case let decl as ClassDeclaration:
            return decl.accessLevelModifier
        case let decl as FunctionDeclaration:
            return decl.modifiers.accessLevel
        case let decl as EnumDeclaration:
            return decl.accessLevelModifier
        case let decl as ConstantDeclaration:
            return decl.modifiers.accessLevel
        case let decl as VariableDeclaration:
            return decl.modifiers.accessLevel
        case let decl as TypealiasDeclaration:
            return decl.accessLevelModifier
        case let decl as StructDeclaration:
            return decl.accessLevelModifier
        case let decl as InitializerDeclaration:
            return decl.modifiers.accessLevel
        case let decl as SubscriptDeclaration:
            return decl.modifiers.accessLevel
        case let decl as ExtensionDeclaration:
            return decl.accessLevelModifier
        case let decl as ProtocolDeclaration:
            return decl.accessLevelModifier
        default:
            return nil
        }
    }
}

extension InitializerDeclaration {
    var fullName: String {
        var value = "init("
        for param in parameterList {
            let label = param.externalName?.textDescription ?? param.localName.textDescription
            let type = param.typeAnnotation.type.textDescription
            value += "\(label)[\(type)]:"
        }
        value += ")"
        return value.replacingOccurrences(of: " ", with: "")
    }
}

extension FunctionDeclaration {
    var fullName: String {
        var value = "\(self.name)("
        for param in signature.parameterList {
            let label = param.externalName?.textDescription ?? param.localName.textDescription
            let type = param.typeAnnotation.type.textDescription
            value += "\(label)[\(type)]:"
        }
        value += ")"
        if self.signature.asyncKind == .async {
            value += "[async]"
        }
        if self.signature.throwsKind != .nothrowing {
            value += "[\(self.signature.throwsKind.textDescription)]"
        }
        return value.replacingOccurrences(of: " ", with: "")
    }
}

extension ProtocolDeclaration.InitializerMember {
    var fullName: String {
        var value = "init("
        for param in parameterList {
            let label = param.externalName?.textDescription ?? param.localName.textDescription
            let type = param.typeAnnotation.type.textDescription
            value += "\(label)[\(type)]:"
        }
        value += ")"
        return value.replacingOccurrences(of: " ", with: "")
    }
}

extension ProtocolDeclaration.MethodMember {
    var fullName: String {
        var value = "\(self.name)("
        for param in signature.parameterList {
            let label = param.externalName?.textDescription ?? param.localName.textDescription
            let type = param.typeAnnotation.type.textDescription
            value += "\(label)[\(type)]:"
        }
        value += ")"
        return value.replacingOccurrences(of: " ", with: "")
    }
}
