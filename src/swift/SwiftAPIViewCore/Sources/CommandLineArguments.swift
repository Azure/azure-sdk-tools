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

class CommandLineArguments: Encodable {
    private var rawArgs: [String: String]

    /// Keys which are supported by Autorest.Swift
    private let supportedKeys: [String] = [
        "source",
        "dest",
        "package-name",
        "package-version"
    ]

    // MARK: Computed properties

    /// Source path to Swift files
    var source: String? {
        return rawArgs["source"]
    }

    /// The desired output path for JSON file
    var dest: String? {
        return rawArgs["dest"]
    }

    var packageName: String? {
        return rawArgs["package-name"]
    }

    var packageVersion: String? {
        return rawArgs["package-version"]
    }

    // MARK: Initializers

    init() {
        rawArgs = [String: String]()

        // Load arguments from direct command line (run standalone)
        for item in CommandLine.arguments.dropFirst() {
            let key = parseKey(from: item)
            if supportedKeys.contains(key) == false {
                SharedLogger.warn("SwiftAPIView does not recognize --\(key)")
            }
            let value = parseValue(from: item)
            rawArgs[key] = value
        }
    }

    /// Returns the argument key without the `--` prefix.
    private func parseKey(from item: String) -> String {
        guard let key = item.split(separator: "=", maxSplits: 1).map({ String($0) }).first else {
            SharedLogger.fail("Item \(item) contained no key.")
        }
        guard key.hasPrefix("--") else {
            SharedLogger.fail("Item \(key) expected to start with -- prefix.")
        }
        return String(key.dropFirst(2))
    }

    private func parseValue(from item: String) -> String {
        let values = Array(item.split(separator: "=", maxSplits: 1).map { String($0) }.dropFirst())
        switch values.count {
        case 0:
            // indicates flag
            return "True"
        case 1:
            return values[0]
        default:
            SharedLogger.fail("Error in item \(item). Expected at most 1 value. Found \(values.count)")
        }
    }
}
