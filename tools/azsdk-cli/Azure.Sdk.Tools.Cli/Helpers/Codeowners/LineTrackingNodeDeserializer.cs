// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Azure.Sdk.Tools.Cli.Models.Codeowners;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.Cli.Helpers.Codeowners;

/// <summary>
/// Records the source line of each mapping node onto the model it produced, so validation errors can
/// point an author at the exact entry rather than at the file as a whole.
/// </summary>
internal sealed class LineTrackingNodeDeserializer(INodeDeserializer inner) : INodeDeserializer
{
    public bool Deserialize(
        IParser reader,
        Type expectedType,
        Func<IParser, Type, object?> nestedObjectDeserializer,
        out object? value,
        ObjectDeserializer rootDeserializer)
    {
        var line = (int)(reader.Current?.Start.Line ?? 0);

        if (!inner.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer))
        {
            return false;
        }

        if (value is IYamlSourceLine tracked)
        {
            tracked.Line = line;
        }

        return true;
    }
}
