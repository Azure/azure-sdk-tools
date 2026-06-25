// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Xml;

namespace Azure.Sdk.Tools.Cli.Services;

/// <summary>
/// Loads XML from untrusted build artifacts.
/// </summary>
public static class XmlSafeLoader
{
    public static async Task<XmlDocument> LoadAsync(string filePath, CancellationToken ct)
    {
        var xmlContent = await File.ReadAllTextAsync(filePath, ct);
        using var stringReader = new StringReader(xmlContent);
        using var reader = XmlReader.Create(stringReader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        var doc = new XmlDocument { XmlResolver = null };
        doc.Load(reader);
        return doc;
    }
}
