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
import Parser
import Source

/// Handles the generation of APIView JSON files.
class APIViewManager {
    // MARK: Properties

    static var shared = APIViewManager()
    let args = CommandLineArguments()
    var tokenFile: TokenFile!

    // MARK: Methods

    func run() throws {
        guard let sourcePath = args.source else {
            SharedLogger.fail("usage error: SwiftAPIView --source PATH")
        }
        guard let sourceUrl = URL(string: args.source ?? sourcePath) else {
            SharedLogger.fail("usage error: `--source PATH` was invalid.")
        }

        try buildTokenFile(from: sourceUrl)

        let destUrl: URL
        if let destPath = args.dest {
            destUrl = URL(fileURLWithPath: destPath)
        } else {
            let destPath = "SwiftAPIView.json"
            guard let dest = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first?.appendingPathComponent(destPath) else {
                SharedLogger.fail("Could not access file system.")
            }
            destUrl = dest
        }
        do {
            let encoder = JSONEncoder()
            encoder.outputFormatting = .prettyPrinted
            let tokenData = try encoder.encode(tokenFile)
            try tokenData.write(to: destUrl)
        } catch {
            SharedLogger.fail(error.localizedDescription)
        }
    }

    /// Handles automatic processing of swiftinterface file, if supplied
    func process(url sourceUrl: URL) throws -> SourceFile {
        guard sourceUrl.absoluteString.hasSuffix("swiftinterface") else {
            return try SourceReader.read(at: sourceUrl.absoluteString)
        }

        func check(line: String) -> String {
            let needsBodyPatterns = [" init(", " init?(", " func ", " deinit"]
            for pattern in needsBodyPatterns {
                if line.contains(pattern) {
                    return "\(line) {}"
                }
            }
            return line
        }

        var newLines = [String]()
        let interfaceContents = try String(contentsOfFile: sourceUrl.absoluteString, encoding: .utf8)
        for line in interfaceContents.components(separatedBy: .newlines) {
            newLines.append(check(line: line))
        }
        // write modified swiftinterface file to temp location
        let sourceDir = sourceUrl.deletingLastPathComponent()
        let filename = sourceUrl.lastPathComponent
        let tempFilename = "\(UUID().uuidString)_\(filename)"
        let tempUrl = sourceDir.appendingPathComponent(tempFilename)
        try newLines.joined(separator: "\n").write(toFile: tempUrl.absoluteString, atomically: true, encoding: .utf8)
        defer { try! FileManager.default.removeItem(atPath: tempUrl.absoluteString) }
        return try SourceReader.read(at: tempUrl.absoluteString)
    }

    func extractPackageName(from sourceUrl: URL) -> String {
        let sourcePath = sourceUrl.path
        let pattern = #"sdk\/[^\/]*\/([^\/]*)"#
        let regex = try! NSRegularExpression(pattern: pattern, options: [])
        let result = regex.matches(in: sourcePath, range: NSMakeRange(0, sourcePath.utf16.count))
        let matchRange = Range(result[0].range(at: 1), in: sourcePath)!
        return String(sourcePath[matchRange])
    }

    func buildTokenFile(from sourceUrl: URL) throws {
        SharedLogger.debug("URL: \(sourceUrl.absoluteString)")
        var declarations = [TopLevelDeclaration]()
        var packageName: String
        var isDir: ObjCBool = false

        guard FileManager.default.fileExists(atPath: sourceUrl.path, isDirectory: &isDir) else {
            SharedLogger.fail("\(sourceUrl.path) does not exist.")
        }

        // collect all swift files in a directory (and subdirectories)
        if isDir.boolValue {
            packageName = extractPackageName(from: sourceUrl)
            let fileEnumerator = FileManager.default.enumerator(atPath: sourceUrl.path)
            while let itemPath = fileEnumerator?.nextObject() as? String {
                guard itemPath.hasSuffix(".swift") else { continue }
                let itemUrl = sourceUrl.appendingPathComponent(itemPath)
                let sourceFile = try SourceReader.read(at: itemUrl.absoluteString)
                let topLevelDecl = try Parser(source: sourceFile).parse()
                declarations.append(topLevelDecl)
            }
        } else {
            // otherwise load a single file
            packageName = sourceUrl.lastPathComponent
            let sourceFile = try process(url: sourceUrl)
            let topLevelDecl = try Parser(source: sourceFile).parse()
            declarations.append(topLevelDecl)
        }
        tokenFile = TokenFile(name: packageName, packageName: packageName, versionString: version)
        tokenFile.process(declarations)
    }
}
