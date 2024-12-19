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

enum CodeDiagnosticLevel: Int {
    case Info = 1
    case Warning = 2
    case Error = 3
    /// Fatal level diagnostic will block API review approval and it will show an error message to the user. Approver will have to
    /// override fatal level system comments before approving a review.*/
    case Fatal = 4
}

class CodeDiagnostic: Tokenizable, Encodable {
    /// The diagnostic ID...?
    var diagnosticId: String?
    /// Id of ReviewLine object where this diagnostic needs to be displayed
    var targetId: String
    /// Auto generated system comment to be displayed under targeted line.
    var text: String
    var level: CodeDiagnosticLevel
    var helpLinkUri: String?

    init(_ text: String, targetId: String, level: CodeDiagnosticLevel, helpLink: String?) {
        self.text = text
        self.targetId = targetId
        self.level = level
        self.helpLinkUri = helpLink
        // FIXME: What is this for?
        self.diagnosticId = nil
    }

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case diagnosticId = "DiagnosticId"
        case targetId = "TargetId"
        case text = "Text"
        case level = "Level"
        case helpLinkUri = "HelpLinkUri"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(targetId, forKey: .targetId)
        try container.encode(text, forKey: .text)
        try container.encode(level.rawValue, forKey: .level)
        try container.encodeIfPresent(helpLinkUri, forKey: .helpLinkUri)
        try container.encodeIfPresent(diagnosticId, forKey: .diagnosticId)
    }

    // MARK: Tokenizable

    func tokenize(apiview: CodeModel, parent: (any Linkable)?) {
        fatalError("Not implemented!")
    }
}
