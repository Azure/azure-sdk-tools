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
        default:
            return nil
        }
    }
}

extension DeclarationModifiers {

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

extension OperatorDeclaration {
    var `operator`: String {
        switch self.kind {
        case let .infix(opName, _):
            return opName
        case let .postfix(opName):
            return opName
        case let .prefix(opName):
            return opName
        }
    }
}

extension ExtensionDeclaration {

    var isPublic: Bool {
        let publicModifiers: [AccessLevelModifier] = [.public, .open]
        return publicModifiers.contains(self.accessLevelModifier ?? .internal)
    }

    var hasPublicMembers: Bool {
        let publicModifiers: [AccessLevelModifier] = [.public, .open]
        for member in self.members {
            switch member {
            case let .declaration(decl):
                guard let accessLevel = decl.accessLevel else { continue }
                if publicModifiers.contains(accessLevel) { return true }
            case .compilerControl(_):
                break
            }
        }
        return false
    }
}
