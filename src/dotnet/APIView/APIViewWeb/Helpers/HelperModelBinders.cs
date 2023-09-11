using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace APIViewWeb.Helpers
{
    public class DecodeModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            var decodedValues = new List<string>();

            foreach (var item in valueProviderResult.Values)
            {
                decodedValues.Add(Uri.UnescapeDataString(item));    
            }

            bindingContext.Result = ModelBindingResult.Success(decodedValues);
            return Task.CompletedTask;
        }
    }
}
