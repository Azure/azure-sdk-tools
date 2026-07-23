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
        using var reader = XmlReader.Create(stringReader, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var doc = new XmlDocument { XmlResolver = null };
        doc.Load(reader);
        return doc;
    }

    /// <summary>
    /// Creates an XmlReader configured to safely read untrusted build artifacts.
    /// </summary>
    public static XmlReader CreateReader(string filePath)
    {
        return XmlReader.Create(filePath, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            Async = true
        });
    }
}
