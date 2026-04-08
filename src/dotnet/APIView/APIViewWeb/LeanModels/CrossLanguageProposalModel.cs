using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using APIViewWeb.Models;

namespace APIViewWeb.LeanModels
{
    public class CrossLanguageProposalModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = IdHelper.GenerateId();
        public string ReviewId { get; set; }
        public string ElementId { get; set; }
        public string CrossLanguageId { get; set; }
        public string ThreadId { get; set; }
        public string ProposalText { get; set; }
        public string Description { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string RoleOfCreator { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsSuperseded { get; set; }
        public string SupersededByProposalId { get; set; }
        public string SupersedeReason { get; set; }
        public string SupersededByUser { get; set; }
        public DateTime? SupersededOn { get; set; }
        public List<ProposalVote> Votes { get; set; } = new List<ProposalVote>();
        public List<ProposalComment> Comments { get; set; } = new List<ProposalComment>();

        /// <summary>
        /// Discriminator field so we can store proposals in the same Cosmos container as comments.
        /// </summary>
        public string Type { get; set; } = "Proposal";
    }

    public class ProposalComment
    {
        public string Id { get; set; } = IdHelper.GenerateId();
        public string CommentText { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string RoleOfCreator { get; set; }
    }
}
