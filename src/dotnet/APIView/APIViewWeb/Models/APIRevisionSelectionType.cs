namespace APIViewWeb.Models;

public enum APIRevisionSelectionType
{
    Undefined = 0,

    /// <summary>
    ///     Use the latest revision regardless of approval status
    /// </summary>
    Latest = 1,

    /// <summary>
    ///     Use the latest approved revision
    /// </summary>
    LatestApproved = 2,

    /// <summary>
    ///     Use the latest manual revision (non-automatic)
    /// </summary>
    LatestAutomatic = 3,

    /// <summary>
    ///     Use the latest manual revision (non-automatic)
    /// </summary>
    LatestManual = 4
}

public enum APIRevisionContentReturnType
{
    Text,
    CodeFile
}
