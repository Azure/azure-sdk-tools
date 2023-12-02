using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using AutoMapper;

namespace APIViewWeb.Helpers
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<UserPreferenceModel, UserPreferenceModel>()
                .ForMember(dest => dest.Language, opt => opt.MapFrom((src, dest) => src._language != null ? src._language : dest._language))
                .ForMember(dest => dest.ApprovedLanguages, opt => opt.MapFrom((src, dest) => src._approvedLanguages != null ? src._approvedLanguages : dest._approvedLanguages))
                .ForMember(dest => dest.APIRevisionType, opt => opt.MapFrom((src, dest) => src._apiRevisionType != null ? src._apiRevisionType : dest._apiRevisionType))
                .ForMember(dest => dest.State, opt => opt.MapFrom((src, dest) => src._state != null ? src._state : dest._state))
                .ForMember(dest => dest.Status, opt => opt.MapFrom((src, dest) => src._status != null ? src._status : dest._status))
                .ForMember(dest => dest.HideLineNumbers, opt => opt.MapFrom((src, dest) => src._hideLineNumbers != null ? src._hideLineNumbers : dest._hideLineNumbers))
                .ForMember(dest => dest.HideLeftNavigation, opt => opt.MapFrom((src, dest) => src._hideLeftNavigation != null ? src._hideLeftNavigation : dest._hideLeftNavigation))
                .ForMember(dest => dest.Theme, opt => opt.MapFrom((src, dest) => src._theme != null ? src._theme : dest._theme))
                .ForMember(dest => dest.ShowHiddenApis, opt => opt.MapFrom((src, dest) => src._showHiddenApis != null ? src._showHiddenApis : dest._showHiddenApis))
                .ForMember(dest => dest.HideReviewPageOptions, opt => opt.MapFrom((src, dest) => src._hideReviewPageOptions != null ? src._hideReviewPageOptions : dest._hideReviewPageOptions))
                .ForMember(dest => dest.HideIndexPageOptions, opt => opt.MapFrom((src, dest) => src._hideIndexPageOptions != null ? src._hideIndexPageOptions : dest._hideIndexPageOptions))
                .ForMember(dest => dest.ShowComments, opt => opt.MapFrom((src, dest) => src._showComments != null ? src._showComments : dest._showComments))
                .ForMember(dest => dest.ShowSystemComments, opt => opt.MapFrom((src, dest) => src._showSystemComments != null ? src._showSystemComments : dest._showSystemComments));
        }
    }
}
