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

/// Struct for setting reivew token options
public struct ReviewTokenOptions {
    /// NavigationDisplayName is used to create a tree node in the navigation panel. Navigation nodes will be created only if token
    /// contains navigation display name.
    var navigationDisplayName: String?

    /// navigateToId should be set if the underlying token is required to be displayed as HREF to another type within the review.
    /// For e.g. a param type which is class name in the same package
    var navigateToId: String?

    /// set skipDiff to true if underlying token needs to be ignored from diff calculation. For e.g. package metadata or dependency
    /// versions are usually excluded when comparing two revisions to avoid reporting them as API changes
    var skipDiff: Bool?

    /// This is set if API is marked as deprecated
    var isDeprecated: Bool?

    /// Set this to true if a prefix space is required before the next value.
    var hasPrefixSpace: Bool?

    /// Set this to true if a suffix space required before next token. For e.g, punctuation right after method name
    var hasSuffixSpace: Bool?

    /// Set isDocumentation to true if current token is part of documentation
    var isDocumentation: Bool?

    /// Language specific style css class names
    var renderClasses: [String]?

    mutating func applySpacing(_ spacing: SpacingKind) {
        switch spacing {
        case .Leading:
            hasPrefixSpace = true
            hasSuffixSpace = false
        case .Trailing:
            hasPrefixSpace = false
            hasSuffixSpace = true
        case .Both:
            hasPrefixSpace = true
            hasSuffixSpace = true
        case .Neither:
            hasPrefixSpace = false
            hasSuffixSpace = false
        }
    }
}

/// Struct for setting line marker options
public struct LineMarkerOptions {
    /// Flag to add the cross language ID
    var addCrossLanguageId: Bool?

    /// Related line ID
    var relatedLineId: String?
}
