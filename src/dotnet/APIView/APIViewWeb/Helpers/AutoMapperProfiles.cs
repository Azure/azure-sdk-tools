using APIViewWeb.Models;
using AutoMapper;
using Microsoft.CodeAnalysis.Diagnostics;

namespace APIViewWeb.Helpers
{
    public class AutoMapperProfiles : Profile
    {
        public AutoMapperProfiles()
        {
            CreateMap<UserPreferenceModel, UserPreferenceModel>()
                .ForMember(dest => dest.Language, opt => opt.MapFrom((src, dest) => src._language != null ? src._language : dest._language))
                .ForMember(dest => dest.ApprovedLanguages, opt => opt.MapFrom((src, dest) => src._approvedLanguages != null ? src._approvedLanguages : dest._approvedLanguages))
                .ForMember(dest => dest.FilterType, opt => opt.MapFrom((src, dest) => src._filterType != null ? src._filterType : dest._filterType))
                .ForMember(dest => dest.State, opt => opt.MapFrom((src, dest) => src._state != null ? src._state : dest._state))
                .ForMember(dest => dest.Status, opt => opt.MapFrom((src, dest) => src._status != null ? src._status : dest._status))
                .ForMember(dest => dest.HideLineNumbers, opt => opt.MapFrom((src, dest) => src._hideLineNumbers != null ? src._hideLineNumbers : dest._hideLineNumbers))
                .ForMember(dest => dest.HideLeftNavigation, opt => opt.MapFrom((src, dest) => src._hideLeftNavigation != null ? src._hideLeftNavigation : dest._hideLeftNavigation))
                .ForMember(dest => dest.Theme, opt => opt.MapFrom((src, dest) => src._theme != null ? src._theme : dest._theme))
                .ForMember(dest => dest.ShowHiddenApis, opt => opt.MapFrom((src, dest) => src._showHiddenApis != null ? src._showHiddenApis : dest._showHiddenApis));

        }
    }
}
