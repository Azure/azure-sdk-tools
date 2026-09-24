using System.Threading.Tasks;
using APIViewWeb.Models;

namespace APIViewWeb.Services
{
    public interface IEmailTemplateService
    {
        Task<string> RenderAsync<TModel>(EmailTemplateKey templateKey, TModel model);
    }
}
