using System;
using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using APIViewWeb.Pages.Assemblies;

namespace APIViewWeb.Helpers
{
    [AttributeUsage(AttributeTargets.Class)]
    sealed public class UploadModelValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
                return new ValidationResult("UploadModel must have a vlaue");

            UploadModel model = value as UploadModel;
            
            if (!String.IsNullOrEmpty(model.Language) && model.Files != null)
            {
                var languageServices = validationContext.GetServices(typeof(LanguageService)) as IEnumerable<LanguageService>;
                var languageService = languageServices.FirstOrDefault(s => (s as LanguageService).Name.Equals(model.Language));
                if (!languageService.IsSupportedFile(model.Files.SingleOrDefault().FileName))
                    return new ValidationResult($"File is invalid for the language selected. Select a file with extension {string.Join(", ", languageService.Extensions)} for language {model.Language}");
            }
            return ValidationResult.Success;
        }
    }
}
