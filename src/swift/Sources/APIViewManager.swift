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
import OrderedCollections
import Parser
import Source
import SourceKittenFramework

/// Handles the generation of APIView JSON files.
class APIViewManager {
    // MARK: Properties

    static var shared = APIViewManager()
    let args = CommandLineArguments()
    var tokenFile: TokenFile!
    var statements = OrderedDictionary<Int, Statement>()

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
    func process(filePath: String) throws -> SourceFile {
        if filePath.hasSuffix("swift") || filePath.hasSuffix("swiftinterface") {
            return try SourceReader.read(at: filePath)
        }
        if filePath.hasSuffix("h") {
            let args = [String]()
            let generated = try Request.interface(file: filePath, uuid: NSUUID().uuidString, arguments: args).send()
            if let match = generated.first(where: { key, _ in
                return key == "key.sourcetext"
            }) {
                let sourceCode = match.value as! String

                // write modified swiftinterface file to temp location
                let fileUrl = URL(fileURLWithPath: filePath)
                let sourceDir = fileUrl.deletingLastPathComponent()
                let filename = String(fileUrl.lastPathComponent.dropLast(2))
                let tempFilename = "\(UUID().uuidString)_\(filename).swiftinterface"
                let tempUrl = sourceDir.appendingPathComponent(tempFilename)
                try sourceCode.write(to: tempUrl, atomically: true, encoding: .utf8)
                defer { try! FileManager.default.removeItem(at: tempUrl) }
                return try SourceReader.read(at: tempUrl.path)
            }
        }
        throw ToolError.client("Unsupported file type: \(filePath)")
    }

    func extractPackageName(from sourceUrl: URL) -> String? {
        let sourcePath = sourceUrl.path
        let pattern = #"sdk\/[^\/]*\/([^\/]*)"#
        let regex = try! NSRegularExpression(pattern: pattern, options: [])
        let result = regex.matches(in: sourcePath, range: NSMakeRange(0, sourcePath.utf16.count))
        guard result.count > 0 else { return nil }
        let matchRange = Range(result[0].range(at: 1), in: sourcePath)!
        return String(sourcePath[matchRange])
    }

    func collectFilePaths(at url: URL) -> [String] {
        var filePaths = [String]()
        let fileEnumerator = FileManager.default.enumerator(atPath: url.path)
        while let item = fileEnumerator?.nextObject() as? String {
            guard item.hasSuffix("swift") || item.hasSuffix("h") || item.hasSuffix("swiftinterface") else { continue }
            filePaths.append(url.appendingPathComponent(item).absoluteString)
        }
        return filePaths
    }

    /// Hashes top-level statement text to eliminate duplicates.
    func merge(statements: [Statement]) {
        statements.forEach { statement in
            self.statements[statement.description.hash] = statement
        }
    }

    func buildTokenFile(from sourceUrl: URL) throws {
        SharedLogger.debug("URL: \(sourceUrl.absoluteString)")
        var packageName: String
        var isDir: ObjCBool = false

        guard FileManager.default.fileExists(atPath: sourceUrl.path, isDirectory: &isDir) else {
            SharedLogger.fail("\(sourceUrl.path) does not exist.")
        }

        var filePaths = [String]()
        if isDir.boolValue {
            packageName = args.packageName ?? extractPackageName(from: sourceUrl) ?? "Default"
            filePaths = collectFilePaths(at: sourceUrl)
        } else {
            packageName = args.packageName ?? sourceUrl.lastPathComponent
            filePaths.append(sourceUrl.absoluteString)
        }
        for filePath in filePaths {
            do {
                let sourceFile = try process(filePath: filePath)
                let topLevelDecl = try Parser(source: sourceFile).parse()
                merge(statements: topLevelDecl.statements)
            } catch let error {
                SharedLogger.warn(error.localizedDescription)
            }
        }
        tokenFile = TokenFile(name: packageName, packageName: packageName, versionString: version)
        tokenFile.process(statements: Array(statements.values))
    }
}
