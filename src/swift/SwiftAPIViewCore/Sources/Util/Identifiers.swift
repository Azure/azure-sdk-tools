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

/// Constructs an identifier based on a simple name
func identifier(forName name: String, withPrefix prefix: String?) -> String {
    var defId = name
    if let prefix = prefix {
        defId = "\(prefix).\(defId)"
    }
    defId = defId.replacingOccurrences(of: " ", with: "_")
    return defId
}


func identifier(forName name: String, withSignature signature: FunctionSignatureSyntax, withPrefix prefix: String?) -> String {
    var defId = name
    var modifiedSignature = signature
    // strip out the internal names and remove spaces
    for (idx, item) in modifiedSignature.input.parameterList.enumerated() {
        var modifiedItem = item
        modifiedItem.secondName = nil
        modifiedSignature.input.parameterList = modifiedSignature.input.parameterList.replacing(childAt: idx, with: modifiedItem)
    }
    let signatureText = modifiedSignature.withoutTrivia().description.filter { !$0.isWhitespace }
    defId = "\(defId)\(signatureText)"
    return identifier(forName: defId, withPrefix: prefix)
}

func identifier(for decl: SubscriptDeclSyntax, withPrefix prefix: String?) -> String {
    var modifiedDecl = decl
    // ignore everything but the signature
    modifiedDecl.accessor = nil
    modifiedDecl.modifiers = nil
    modifiedDecl.attributes = nil
    let defId = modifiedDecl.withoutTrivia().description.filter { !$0.isWhitespace }
    return identifier(forName: defId, withPrefix: prefix)
}
