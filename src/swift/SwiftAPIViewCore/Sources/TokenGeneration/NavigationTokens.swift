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

extension TokenFile {

    // MARK: Navigation Emitter Methods

    func navigationTokens(from statements: [Statement], withPrefix prefix: String) -> [NavigationItem] {
        var navItems: [NavigationItem] = []
        statements.forEach { stmt in
            if let item = navigationTokens(from: stmt, withPrefix: prefix) {
                navItems.append(item)
            }
        }
        return navItems
    }

    private func navigationTokens(from decl: ClassDeclaration, withPrefix prefix: String) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(name: decl.name.textDescription, prefix: prefix, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(memberDecl):
                let childPrefix = "\(prefix).\(decl.name.textDescription)"
                if let item = navigationTokens(from: memberDecl, withPrefix: childPrefix) {
                    navItem.childItems.append(item)
                }
            case .compilerControl:
                return
            }
        }
        return navItem
    }

    private func navigationTokens(from decl: StructDeclaration, withPrefix prefix: String) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(name: decl.name.textDescription, prefix: prefix, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let item = navigationTokens(from: decl, withPrefix: navItem.navigationId) {
                    navItem.childItems.append(item)
                }
            case .compilerControl:
                return
            }
        }
        return navItem
    }

    private func navigationTokens(from decl: EnumDeclaration, withPrefix prefix: String) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(name: decl.name.textDescription, prefix: prefix, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(memberDecl):
                if let item = navigationTokens(from: memberDecl, withPrefix: navItem.navigationId) {
                    navItem.childItems.append(item)
                }
            default:
                return
            }
        }
        return navItem
    }

    private func navigationTokens(from decl: ProtocolDeclaration, withPrefix prefix: String) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(name: decl.name.textDescription, prefix: prefix, typeKind: .class)
        for item in decl.members {
            if case let ProtocolDeclaration.Member.associatedType(data) = item {
                navItem.childItems.append(NavigationItem(name: data.name.textDescription, prefix: navItem.navigationId, typeKind: .class))
            }
        }
        return navItem
    }

    private func navigationTokens(from decl: ExtensionDeclaration, withPrefix prefix: String) -> NavigationItem? {
        // FIXME: extensions should not have navigation tokens by themselves.
        // If the extension contains something that should be shown in navigation,
        // it should be associated with the type it extends.
        return nil
    }

    private func navigationTokens(from decl: Statement, withPrefix prefix: String) -> NavigationItem? {
        switch decl {
        case let decl as ClassDeclaration:
            return navigationTokens(from: decl, withPrefix: prefix)
        case let decl as EnumDeclaration:
            return navigationTokens(from: decl, withPrefix: prefix)
        case let decl as ExtensionDeclaration:
            return navigationTokens(from: decl, withPrefix: prefix)
        case let decl as ProtocolDeclaration:
            return navigationTokens(from: decl, withPrefix: prefix)
        case let decl as StructDeclaration:
            return navigationTokens(from: decl, withPrefix: prefix)
        default:
            return nil
        }
    }
}
