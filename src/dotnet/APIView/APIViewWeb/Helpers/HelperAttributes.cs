using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using APIViewWeb.Pages.Assemblies;

namespace APIViewWeb.Helpers
{
    [AttributeUsage(AttributeTargets.Class)]
    sealed public class UploadModelValidationAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
                return new ValidationResult("UploadModel must have a value");

            UploadModel model = value as UploadModel;
            
            if (!String.IsNullOrEmpty(model.Language) && model.Files != null)
            {
                var languageServices = validationContext.GetServices(typeof(LanguageService)) as IEnumerable<LanguageService>;
                var languageService = languageServices.FirstOrDefault(s => (s as LanguageService).Name.Equals(model.Language));
                var fileName = model.Files.SingleOrDefault().FileName;
                var errorMessage = $"File is invalid for the language selected. Select a file with extension {string.Join(", ", languageService.Extensions)} for language {model.Language}";

                if (model.Language == "Swift" && !fileName.EndsWith(".json"))
                    return new ValidationResult(errorMessage);

                if (model.Language != "Swift" && !languageService.IsSupportedFile(fileName))
                    return new ValidationResult(errorMessage);
            }
            return ValidationResult.Success;
        }
    }
}
