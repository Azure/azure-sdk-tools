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
import AST

class APIViewModel: Tokenizable, Codable {

    /// The name to be used in the APIView review list
    var name: String

    /// The package name used by APIView
    var packageName: String

    /// The version string
    var versionString: String

    /// Language string.
    let language = "Swift"

    /// The APIView tokens to display
    var tokens: [Token]

    /// The navigation tokens to display
    var navigation: [NavigationToken]

    // MARK: Initializers

    init(name: String, packageName: String, versionString: String, statements: [Statement]) {
        self.name = name
        self.versionString = versionString
        self.packageName = packageName
        let package = PackageModel(name: packageName, statements: statements)
        self.navigation = package.navigationTokenize(parent: nil)
        self.tokens = [Token]()
        self.tokens.append(contentsOf: self.tokenize())
        self.tokens.append(contentsOf: package.tokenize())
    }

    // MARK: Codable

    enum CodingKeys: String, CodingKey {
        case name = "Name"
        case tokens = "Tokens"
        case language = "Language"
        case packageName = "PackageName"
        case navigation = "Navigation"
        case versionString = "VersionString"
    }

    func encode(to encoder: Encoder) throws {
        var container = encoder.container(keyedBy: CodingKeys.self)
        try container.encode(name, forKey: .name)
        try container.encode(packageName, forKey: .packageName)
        try container.encode(language, forKey: .language)
        try container.encode(tokens, forKey: .tokens)
        try container.encode(navigation, forKey: .navigation)
        try container.encode(versionString, forKey: .versionString)
    }

    func tokenize() -> [Token] {
        // Renders the APIView "preamble"
        var t = [Token]()
        let bundle = Bundle(for: Swift.type(of: self))
        let versionKey = "CFBundleShortVersionString"
        let apiViewVersion = bundle.object(forInfoDictionaryKey: versionKey) as? String ?? "Unknown"
        t.text("Package parsed using Swift APIView (version \(apiViewVersion))")
        t.newLine()
        t.newLine()
        return t
    }
}
