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
                .ForMember(dest => dest.UserName, opt => opt.MapFrom((src, dest) => src.UserName != null ? src.UserName : dest.UserName))
                .ForMember(dest => dest.Language, opt => opt.MapFrom((src, dest) => src.Language != null ? src.Language : dest.Language))
                .ForMember(dest => dest.FilterType, opt => opt.MapFrom((src, dest) => src.FilterType != null ? src.FilterType : dest.FilterType))
                .ForMember(dest => dest.State, opt => opt.MapFrom((src, dest) => src.State != null ? src.State : dest.State))
                .ForMember(dest => dest.Status, opt => opt.MapFrom((src, dest) => src.Status != null ? src.Status : dest.Status))
                .ForMember(dest => dest.HideLineNumbers, opt => opt.MapFrom((src, dest) => src.HideLineNumbers != null ? src.HideLineNumbers : dest.HideLineNumbers))
                .ForMember(dest => dest.HideLeftNavigation, opt => opt.MapFrom((src, dest) => src.HideLeftNavigation != null ? src.HideLeftNavigation : dest.HideLeftNavigation))
                .ForMember(dest => dest.Theme, opt => opt.MapFrom((src, dest) => src.Theme != null ? src.Theme : dest.Theme));
        }
    }
}
