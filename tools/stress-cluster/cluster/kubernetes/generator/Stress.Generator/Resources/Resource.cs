using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Stress.Generator
{
    public abstract class Resource
    {
        public string TemplatePath;
        public string Template;
        public bool IsLoaded = false;
        
        public Resource()
        {
        }
        
        private void CheckLoaded()
        {
            if (!IsLoaded)
            {
                throw new Exception("Template is not loaded");
            }
        }
        
        public IEnumerable<ResourcePropertyInfo> Properties()
        {
            return this.GetType().GetProperties()
                   .Where(p => p.GetCustomAttribute(typeof(ResourceProperty)) != null)
                   .Select(p => {
                       var prop = p.GetCustomAttribute(typeof(ResourceProperty)) as ResourceProperty;
                       return new ResourcePropertyInfo(p, prop.Help);
                   });
        }

        public IEnumerable<ResourcePropertyInfo> OptionalProperties()
        {
            return this.GetType().GetProperties()
                   .Where(p => p.GetCustomAttribute(typeof(OptionalResourceProperty)) != null)
                   .Select(p => {
                       var prop = p.GetCustomAttribute(typeof(OptionalResourceProperty)) as OptionalResourceProperty;
                       return new ResourcePropertyInfo(p, prop.Help);
                   });
        }
        
        public void SetProperty(PropertyInfo prop, object value)
        {
            prop.SetValue(this, value);
        }

        public void Load()
        {
            Template = "";
            IsLoaded = true;
        }

        public string Render()
        {
            CheckLoaded();
            return "";
        }

        public void Write()
        {
            CheckLoaded();
            var rendered = Render();
        }
    }
}
