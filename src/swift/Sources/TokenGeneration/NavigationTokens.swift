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

    func navigationTokens(from declarations: [TopLevelDeclaration]) {
        let packageNavItem = NavigationItem(text: packageName, navigationId: nil, typeKind: .assembly)
        declarations.forEach { decl in
            let item = navigationTokens(from: decl)
            packageNavItem.childItems += item
        }
        navigation = [packageNavItem]
    }

    private func navigationTokens(from decl: TopLevelDeclaration) -> [NavigationItem] {
        var navItems: [NavigationItem] = []
        decl.statements.forEach { stmt in
            if let item = navigationTokens(from: stmt) {
                navItems.append(item)
            }
        }
        return navItems
    }

    private func navigationTokens(from decl: ClassDeclaration) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(text: decl.name.textDescription, navigationId: decl.name.textDescription, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let item = navigationTokens(from: decl) {
                    navItem.childItems.append(item)
                }
            case .compilerControl:
                return
            }
        }
        return navItem
    }

    private func navigationTokens(from decl: StructDeclaration) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(text: decl.name.textDescription, navigationId: decl.name.textDescription, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let item = navigationTokens(from: decl) {
                    navItem.childItems.append(item)
                }
            case .compilerControl:
                return
            }
        }
        return navItem
    }

    private func navigationTokens(from decl: EnumDeclaration) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(text: decl.name.textDescription, navigationId: decl.name.textDescription, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let item = navigationTokens(from: decl) {
                    navItem.childItems.append(item)
                }
            default:
                return
            }
        }
        return navItem
    }

    private func navigationTokens(from decl: ProtocolDeclaration) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(text: decl.name.textDescription, navigationId: decl.name.textDescription, typeKind: .class)
        return navItem
    }

    private func navigationTokens(from decl: ExtensionDeclaration) -> NavigationItem? {
        guard publicModifiers.contains(decl.accessLevelModifier ?? .internal) else {
            return nil
        }
        let navItem = NavigationItem(text: decl.type.textDescription, navigationId: decl.type.textDescription, typeKind: .class)
        decl.members.forEach { member in
            switch member {
            case let .declaration(decl):
                if let item = navigationTokens(from: decl) {
                    navItem.childItems.append(item)
                }
            default:
                return
            }
        }
        return navItem
    }

    private func navigationTokens(from decl: Statement) -> NavigationItem? {
        switch decl {
        case let decl as ClassDeclaration:
            return navigationTokens(from: decl)
        case let decl as EnumDeclaration:
            return navigationTokens(from: decl)
        case let decl as ExtensionDeclaration:
            return navigationTokens(from: decl)
        case let decl as ProtocolDeclaration:
            return navigationTokens(from: decl)
        case let decl as StructDeclaration:
            return navigationTokens(from: decl)
        default:
            return nil
        }
    }
}
