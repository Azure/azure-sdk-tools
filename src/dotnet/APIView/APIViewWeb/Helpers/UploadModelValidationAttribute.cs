using System;
using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.CodeAnalysis.Host;
using static Microsoft.VisualStudio.Services.Graph.Constants;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace APIViewWeb.Helpers
{
    [AttributeUsage(AttributeTargets.Class)]
    sealed public class UploadModelValidationAttribute : ValidationAttribute
    {
        public string PropertyOneName { get; set; }
        public string PropertyTwoName { get; set; }

        public UploadModelValidationAttribute(string propertyOneName, string propertyTwoName)
        {
            PropertyOneName = propertyOneName;
            PropertyTwoName = propertyTwoName;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value == null)
                return new ValidationResult("UploadModel must have a vlaue");

            Object instance = validationContext.ObjectInstance;
            Type type = instance.GetType();

            var language = type.GetProperty(PropertyOneName).GetValue(instance) as string;
            var files = type.GetProperty(PropertyOneName).GetValue(instance) as IFormFile[];

            if (!String.IsNullOrEmpty(language) && files != null)
            {
                var languageServices = validationContext.GetServices(typeof(LanguageService)) as IEnumerable<LanguageService>;
                var languageService = languageServices.FirstOrDefault(s => (s as LanguageService).Name.Equals(language));
                if (!languageService.IsSupportedFile(files.SingleOrDefault().FileName))
                    return new ValidationResult($"File is invlaid for the language selected. For language {language} select a file with extension {string.Join(", ", languageService.Extensions)}");
            }
            return ValidationResult.Success;
        }
    }
}
