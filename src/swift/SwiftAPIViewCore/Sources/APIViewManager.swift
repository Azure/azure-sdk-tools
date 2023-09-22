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
import OrderedCollections
import SwiftSyntax
import SwiftSyntaxParser
import SourceKittenFramework


public enum APIViewManagerMode {
    case testing
    case commandLine
}

public class APIViewConfiguration {
    public var sourcePath: String = ""

    public var destPath: String? = nil

    public var packageName: String? = nil

    public var packageVersion: String? = nil

    public func loadCommandLineArgs() {
        let args = CommandLineArguments()
        guard let source = args.source else {
            SharedLogger.fail("usage error: SwiftAPIView --source PATH")
        }
        self.sourcePath = source
        self.destPath = args.dest ?? self.destPath
        self.packageName = args.packageName ?? self.packageName
        self.packageVersion = args.packageVersion ?? self.packageVersion
    }
}

/// Handles the generation of APIView JSON files.
public class APIViewManager: SyntaxVisitor {

    // MARK: Properties

    var config = APIViewConfiguration()

    var mode: APIViewManagerMode

    var statements = OrderedDictionary<Int, CodeBlockItemSyntax.Item>()

    // MARK: Initializer

    public init(mode: APIViewManagerMode = .commandLine) {
        self.mode = mode
        super.init(viewMode: .all)
    }

    // MARK: Methods

    public func run() throws -> String {
        if mode == .commandLine {
            config.loadCommandLineArgs()
        }
        guard let sourceUrl = URL(string: config.sourcePath) else {
            SharedLogger.fail("usage error: source path was invalid.")
        }
        let apiView = try createApiView(from: sourceUrl)

        switch mode {
        case .commandLine:
            save(apiView: apiView)
            return ""
        case .testing:
            return apiView.text
        }
    }

    /// Persist the token file to disk
    func save(apiView: APIViewModel) {
        let destUrl: URL
        if let destPath = config.destPath {
            destUrl = URL(fileURLWithPath: destPath)
        } else {
            let destPath = "\(config.packageName!)_swift.json"
            guard let dest = FileManager.default.urls(for: .documentDirectory, in: .userDomainMask).first?.appendingPathComponent(destPath) else {
                SharedLogger.fail("Could not access file system.")
            }
            destUrl = dest
        }
        do {
            let encoder = JSONEncoder()
            encoder.outputFormatting = .prettyPrinted
            let tokenData = try encoder.encode(apiView)
            try tokenData.write(to: destUrl)
        } catch {
            SharedLogger.fail(error.localizedDescription)
        }
    }

    /// Handles automatic processing of swiftinterface file, if supplied
    func process(filePath: String) throws -> String {
        if filePath.hasSuffix("swift") || filePath.hasSuffix("swifttxt") || filePath.hasSuffix("swiftinterface") {
            return try String(contentsOfFile: filePath)
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
                return try String(contentsOfFile: tempUrl.path)
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

    func extractPackageVersion(from sourceUrl: URL) -> String? {
        let fileManager = FileManager.default
        let rootUrl = sourceUrl.deletingLastPathComponent()
        guard let enumerator = fileManager.enumerator(
            at: rootUrl,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles]
        ) else {
            return nil
        }
        while let element = enumerator.nextObject() as? URL {
            if element.path.hasSuffix("podspec.json") {
                do {
                    let data = try Data(contentsOf: element)
                    let json = try JSONSerialization.jsonObject(with: data) as? Dictionary<String, AnyObject>
                    let version = json?["version"] as? String
                    return version
                } catch {
                    return nil
                }
            }
        }
        return nil
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

    func createApiView(from sourceUrl: URL) throws -> APIViewModel {
        SharedLogger.debug("URL: \(sourceUrl.absoluteString)")
        var packageName: String?
        var packageVersion: String?
        var isDir: ObjCBool = false

        guard FileManager.default.fileExists(atPath: sourceUrl.path, isDirectory: &isDir) else {
            SharedLogger.fail("\(sourceUrl.path) does not exist.")
        }

        var filePaths = [String]()
        if isDir.boolValue {
            packageName = config.packageName ?? extractPackageName(from: sourceUrl)
            packageVersion = config.packageVersion ?? extractPackageVersion(from: sourceUrl)
            filePaths = collectFilePaths(at: sourceUrl)
        } else {
            packageName = config.packageName ?? sourceUrl.lastPathComponent
            packageVersion = config.packageVersion
            filePaths.append(sourceUrl.absoluteString)
        }
        for filePath in filePaths {
            do {
                let source = try process(filePath: filePath)
                let rootNode = try SyntaxParser.parse(source: source);
                self.walk(rootNode)
            } catch let error {
                SharedLogger.warn("\(error)")
            }
        }
        // Ensure that package name and version can be resolved
        var errors = [String]()
        if packageName == nil {
            errors.append("Unable to resolve package name. Use --package-name NAME")
        }
        if packageVersion == nil {
            errors.append("Unable to resolve package version. Use --package-version VERSION")
        }
        if !errors.isEmpty {
            let failString = errors.joined(separator: "\n")
            SharedLogger.fail(failString)
        }
        config.packageName = packageName!
        config.packageVersion = packageVersion!
        let apiViewName = "\(packageName!) (version \(packageVersion!))"
        let apiView = APIViewModel(name: apiViewName, packageName: packageName!, versionString: packageVersion!, statements: Array(statements.values))
        return apiView
    }

    // MARK: SyntaxVisitor
    
    public override func visit(_ node: SourceFileSyntax) -> SyntaxVisitorContinueKind {
        for statement in node.statements {
            let tokens = Array(statement.item.tokens(viewMode: .all))
            statements[tokens.hashValue] = statement.item
        }
        return .skipChildren
    }
}

