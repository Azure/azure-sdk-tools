using System;

namespace APIViewWeb.Models;

public class MarkReleasedResult
{
    public string ReviewId { get; set; }
    public string RevisionId { get; set; }
    public bool IsReleased { get; set; }
    public DateTime? ReleasedOn { get; set; }
}