using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace APIViewWeb.LeanControllers
{
    public class ProposalsController : BaseApiController
    {
        private readonly ICosmosProposalsRepository _proposalsRepository;
        private readonly IPermissionsManager _permissionsManager;

        public ProposalsController(
            ICosmosProposalsRepository proposalsRepository,
            IPermissionsManager permissionsManager)
        {
            _proposalsRepository = proposalsRepository;
            _permissionsManager = permissionsManager;
        }

        /// <summary>
        /// Get all proposals for a given crossLanguageId
        /// </summary>
        [HttpGet("byCrossLanguageId")]
        public async Task<ActionResult> GetProposalsByCrossLanguageId([FromQuery] string crossLanguageId)
        {
            var proposals = await _proposalsRepository.GetProposalsByCrossLanguageIdAsync(crossLanguageId);
            return new LeanJsonResult(proposals, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Get all proposals for a given reviewId
        /// </summary>
        [HttpGet("byReview/{reviewId}")]
        public async Task<ActionResult> GetProposalsByReviewId(string reviewId)
        {
            var proposals = await _proposalsRepository.GetProposalsByReviewIdAsync(reviewId);
            return new LeanJsonResult(proposals, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Create a new cross-language proposal
        /// </summary>
        [HttpPost]
        public async Task<ActionResult> CreateProposalAsync(
            [FromForm] string reviewId,
            [FromForm] string elementId,
            [FromForm] string crossLanguageId,
            [FromForm] string proposalText,
            [FromForm] string threadId = null,
            [FromForm] string description = null)
        {
            if (string.IsNullOrEmpty(reviewId) || string.IsNullOrEmpty(crossLanguageId) || string.IsNullOrEmpty(proposalText))
            {
                return BadRequest();
            }

            var userName = User.GetGitHubLogin();
            string roleOfCreator = null;
            try
            {
                var permissions = await _permissionsManager.GetEffectivePermissionsAsync(userName);
                roleOfCreator = ResolveRoleLabel(permissions);
            }
            catch { }

            var proposal = new CrossLanguageProposalModel
            {
                ReviewId = reviewId,
                ElementId = elementId,
                CrossLanguageId = crossLanguageId,
                ThreadId = threadId,
                ProposalText = proposalText,
                Description = description,
                CreatedBy = userName,
                CreatedOn = DateTime.UtcNow,
                RoleOfCreator = roleOfCreator
            };

            await _proposalsRepository.UpsertProposalAsync(proposal);
            return new LeanJsonResult(proposal, StatusCodes.Status201Created);
        }

        /// <summary>
        /// Vote on a proposal
        /// </summary>
        [HttpPatch("{reviewId}/{proposalId}/vote")]
        public async Task<ActionResult> VoteOnProposalAsync(
            string reviewId,
            string proposalId,
            [FromForm] string language,
            [FromForm] string decision,
            [FromForm] string modificationText = null)
        {
            var proposal = await _proposalsRepository.GetProposalAsync(reviewId, proposalId);
            if (proposal == null) return NotFound();

            if (!Enum.TryParse<ProposalDecision>(decision, true, out var parsedDecision))
                return BadRequest("Invalid decision value");

            var userName = User.GetGitHubLogin();
            string voterRole = null;
            try
            {
                var permissions = await _permissionsManager.GetEffectivePermissionsAsync(userName);
                voterRole = ResolveRoleLabel(permissions);
            }
            catch { }

            // One vote per language — replace existing
            proposal.Votes.RemoveAll(v => v.Language == language);
            proposal.Votes.Add(new ProposalVote
            {
                Language = language,
                Decision = parsedDecision,
                ModificationText = modificationText,
                VotedBy = userName,
                VoterRole = voterRole,
                VotedOn = DateTime.UtcNow
            });

            await _proposalsRepository.UpsertProposalAsync(proposal);
            return new LeanJsonResult(proposal, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Add a comment to a proposal
        /// </summary>
        [HttpPost("{reviewId}/{proposalId}/comments")]
        public async Task<ActionResult> AddProposalCommentAsync(
            string reviewId,
            string proposalId,
            [FromForm] string commentText)
        {
            if (string.IsNullOrEmpty(commentText)) return BadRequest();

            var proposal = await _proposalsRepository.GetProposalAsync(reviewId, proposalId);
            if (proposal == null) return NotFound();

            var userName = User.GetGitHubLogin();
            string roleOfCreator = null;
            try
            {
                var permissions = await _permissionsManager.GetEffectivePermissionsAsync(userName);
                roleOfCreator = ResolveRoleLabel(permissions);
            }
            catch { }

            proposal.Comments.Add(new ProposalComment
            {
                CommentText = commentText,
                CreatedBy = userName,
                CreatedOn = DateTime.UtcNow,
                RoleOfCreator = roleOfCreator
            });

            await _proposalsRepository.UpsertProposalAsync(proposal);
            return new LeanJsonResult(proposal, StatusCodes.Status200OK);
        }

        /// <summary>
        /// Delete a proposal (soft delete)
        /// </summary>
        [HttpDelete("{reviewId}/{proposalId}")]
        public async Task<ActionResult> DeleteProposalAsync(string reviewId, string proposalId)
        {
            var proposal = await _proposalsRepository.GetProposalAsync(reviewId, proposalId);
            if (proposal == null) return NotFound();

            proposal.IsDeleted = true;
            await _proposalsRepository.UpsertProposalAsync(proposal);
            return Ok();
        }

        /// <summary>
        /// Supersede a proposal — marks the old proposal as superseded with a reason,
        /// preserving its vote history for reference.
        /// </summary>
        [HttpPatch("{reviewId}/{proposalId}/supersede")]
        public async Task<ActionResult> SupersedeProposalAsync(
            string reviewId,
            string proposalId,
            [FromForm] string reason)
        {
            var proposal = await _proposalsRepository.GetProposalAsync(reviewId, proposalId);
            if (proposal == null) return NotFound();

            var userName = User.GetGitHubLogin();

            proposal.IsSuperseded = true;
            proposal.SupersedeReason = reason;
            proposal.SupersededByUser = userName;
            proposal.SupersededOn = DateTime.UtcNow;

            await _proposalsRepository.UpsertProposalAsync(proposal);
            return new LeanJsonResult(proposal, StatusCodes.Status200OK);
        }

        private static string ResolveRoleLabel(EffectivePermissions permissions)
        {
            if (permissions?.Roles == null) return null;

            var langRole = permissions.Roles.OfType<LanguageScopedRoleAssignment>().FirstOrDefault();
            if (langRole != null)
            {
                var roleName = langRole.Role == LanguageScopedRole.Architect ? "Architect" : "Deputy Architect";
                return $"{langRole.Language} {roleName}";
            }

            var globalRole = permissions.Roles.OfType<GlobalRoleAssignment>().FirstOrDefault();
            if (globalRole != null)
            {
                return globalRole.Role switch
                {
                    GlobalRole.Admin => "Admin",
                    GlobalRole.SdkTeam => "SDK Team",
                    GlobalRole.ServiceTeam => "Service Team",
                    _ => null
                };
            }

            return null;
        }
    }
}
