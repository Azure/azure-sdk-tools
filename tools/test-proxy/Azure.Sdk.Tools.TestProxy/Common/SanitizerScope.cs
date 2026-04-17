using System;

namespace Azure.Sdk.Tools.TestProxy.Common;

/// <summary>
/// Flags indicating which parts of a RecordEntry a sanitizer applies to.
/// Used to optimize sanitization by only running relevant methods.
/// </summary>
[Flags]
public enum SanitizerScope
{
    None = 0,
    Header = 1 << 0,     // 0x1
    Body = 1 << 1,       // 0x2
    Uri = 1 << 2,        // 0x4
    All = Header | Body | Uri
}
