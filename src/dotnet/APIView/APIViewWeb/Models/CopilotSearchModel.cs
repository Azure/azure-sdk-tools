
using System.Reflection.Metadata;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace APIViewWeb.Models
{
    public class CopilotSearchModel
    {
        public float similarity { get; set; }
        public CopilotCommentModel document { get; set; }

        public CopilotSearchModel(float similarity, CopilotCommentModel document)
        {
            this.similarity = similarity;
            this.document = CreateCopyWithoutEmbedding(document);
        }

        private static CopilotCommentModel CreateCopyWithoutEmbedding(CopilotCommentModel model)
        {
            return new CopilotCommentModel()
            {
                Id = model.Id,
                BadCode = model.BadCode,
                GoodCode = model.GoodCode,
                Embedding = null,
                Language = model.Language,
                Comment = model.Comment,
                GuidelineIds = model.GuidelineIds,
                ModifiedOn = model.ModifiedOn,
                ModifiedBy = model.ModifiedBy,
                IsDeleted = model.IsDeleted
            };
        }
    }
}
