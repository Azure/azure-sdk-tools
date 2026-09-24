using System.ComponentModel.DataAnnotations;

namespace APIViewWeb.Models;

public class ApprovalRequest
{
    [Required] 
    public bool Approve { get; set; }
}
