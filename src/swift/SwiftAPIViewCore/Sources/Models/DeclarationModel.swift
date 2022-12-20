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
import SwiftSyntax


class DeclarationModel: Tokenizable, Linkable {

    var accessLevel: AccessLevel
    var name: String
    var definitionId: String?
    var lineId: String?
    var parent: Linkable?
    let childNodes: SyntaxChildren

    init(from decl: hasIdentifier & hasModifiers & hasChildren, parent: Linkable) {
        self.parent = parent
        let name = decl.identifier.withoutTrivia().text
        definitionId = identifier(forName: name, withPrefix: parent.definitionId)
        lineId = nil
        self.name = name
        self.childNodes = decl.children(viewMode: .sourceAccurate)
        self.accessLevel = decl.modifiers.accessLevel
    }

    init(from decl: DeclSyntax, parent: Linkable) {
        SharedLogger.warn("Unexpected declaration type: \(decl.kind). This may not appear correctly in APIView.")
        self.parent = parent
        let name = (decl as? hasIdentifier)?.identifier.withoutTrivia().text ?? ""
        definitionId = identifier(forName: name, withPrefix: parent.definitionId)
        lineId = nil
        self.name = name
        self.childNodes = decl.children(viewMode: .sourceAccurate)
        self.accessLevel = (decl as? hasModifiers)?.modifiers.accessLevel ?? .unspecified
    }

    func tokenize(apiview a: APIViewModel, parent: Linkable?) {
        guard APIViewModel.publicModifiers.contains(accessLevel) else { return }
        for child in childNodes {
            switch child.kind {
            case .token:
                if child.withoutTrivia().description == self.name {
                    a.typeDeclaration(name: self.name, definitionId: self.definitionId)
                } else {
                    child.tokenize(apiview: a, parent: nil)
                }
            default:
                // call default implementation in SyntaxProtocol+Extensions
                child.tokenize(apiview: a, parent: self)
            }
        }
    }

    func navigationTokenize(apiview a: APIViewModel) {
        guard APIViewModel.publicModifiers.contains(accessLevel) else { return }
        a.add(token: NavigationToken(name: name, prefix: parent?.name, typeKind: .class))
        // TODO: Restore nested links
    }
}
