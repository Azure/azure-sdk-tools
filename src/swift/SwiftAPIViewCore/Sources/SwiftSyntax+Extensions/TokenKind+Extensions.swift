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

enum SpacingKind {
    /// Leading space
    case Leading
    /// Trailing space
    case Trailing
    /// Leading and trailing space
    case Both
    /// No spacing
    case Neither
    /// chomps any leading whitespace to the left
    case TrimLeft
}

extension SwiftSyntax.TokenKind {
    var spacing: SpacingKind {
        switch self {
        case .anyKeyword: return .Neither
        case .nilKeyword: return .Neither
        case .prefixPeriod: return .Leading
        case .comma: return .Trailing
        case .colon: return .Trailing
        case .semicolon: return .Trailing
        case .equal: return .Both
        case .arrow: return .Both
        case .postfixQuestionMark: return .TrimLeft
        case .leftBrace: return .Leading
        case .initKeyword: return .Leading
        case .wildcardKeyword: return .Neither
        case let .contextualKeyword(val):
            switch val {
            case "objc": return .TrimLeft
            case "lowerThan", "higherThan", "associativity": return .Neither
            case "available", "unavailable", "introduced", "deprecated", "obsoleted", "message", "renamed": return .Neither
            default: return .Both
            }
        default:
            return self.isKeyword ? .Both : .Neither
        }
    }
}
