using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace EasyNms.Configuration
{
    [ConfigurationCollection(typeof(PropertyElement))]
    public class PropertyElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new PropertyElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((PropertyElement)element).Name;
        }
    }
}
