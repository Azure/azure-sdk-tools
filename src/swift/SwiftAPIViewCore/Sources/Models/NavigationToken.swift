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

import Foundation

/// APIView navigation toekn for the left-hand navigation sidebar
class NavigationToken: Codable {
    /// Name to display in the navigation sidebar
    var name: String
    /// Unique indentifier describing the navigation path
    var navigationId: String
    /// Child navigation items
    var childItems = [NavigationToken]()
    /// Tags which determine the type of icon displayed in the navigation pane of APIView
    var tags: NavigationTags

    init(name: String, navigationId: String, typeKind: NavigationTypeKind, members: [DeclarationModel]) {
        self.name = name
        self.navigationId = navigationId
        tags = NavigationTags(typeKind: typeKind)
        for member in members {
            let memberNavId = "\(navigationId).\(member.name)"
            let memberNav = NavigationToken(name: member.name, navigationId: memberNavId, typeKind: member.kind.navigationSymbol, members: [])
            childItems.append(memberNav)
        }
        // sort the navigation elements by name
        childItems.sort { $0.name < $1.name }
    }

    init(name: String, navigationId: String, typeKind: NavigationTypeKind, extensions: [ExtensionModel]) {
        self.name = name
        self.navigationId = navigationId
        tags = NavigationTags(typeKind: typeKind)
        for ext in extensions {
            let extNav = NavigationToken(name: ext.extendedType, navigationId: ext.definitionId, typeKind: .class, members: [])
            childItems.append(extNav)
        }
        // sort the navigation elements by name
        childItems.sort { $0.name < $1.name }
    }

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case name = "Text"
        case navigationId = "NavigationId"
        case childItems = "ChildItems"
        case tags = "Tags"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(name, forKey: .name)
        try container.encode(navigationId, forKey: .navigationId)
        try container.encode(childItems, forKey: .childItems)
        try container.encode(tags, forKey: .tags)
    }
}
