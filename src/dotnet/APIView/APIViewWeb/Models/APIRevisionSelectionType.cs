namespace APIViewWeb.Models;

public enum APIRevisionSelectionType
{
    /// <summary>
    ///     Use the specific API revision ID provided in activeApiRevisionId parameter
    /// </summary>
    Specific = 0,

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
    LatestManual = 3
}
