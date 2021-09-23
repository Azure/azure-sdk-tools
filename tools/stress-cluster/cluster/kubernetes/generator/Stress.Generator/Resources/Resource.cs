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
        
        public Resource(string templatePath)
        {
            TemplatePath = templatePath;
        }
        
        private void CheckLoaded()
        {
            if (!IsLoaded)
            {
                throw new Exception("Template is not loaded");
            }
        }
        
        public IEnumerable<PropertyInfo> Properties()
        {
            return this.GetType().GetProperties().Where(p =>
                p.GetCustomAttributes(typeof(ResourceProperty)).Count() > 0
            );
        }

        public IEnumerable<PropertyInfo> OptionalProperties()
        {
            return this.GetType().GetProperties().Where(p =>
                p.GetCustomAttributes(typeof(OptionalResourceProperty)).Count() > 0
            );
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
