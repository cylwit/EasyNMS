using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace EasyNms.Configuration
{
    [ConfigurationCollection(typeof(EndPointElement))]
    public class EndPointElementCollection : ConfigurationElementCollection
    {
        protected override ConfigurationElement CreateNewElement()
        {
            return new EndPointElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((EndPointElement)element).Uri;
        }
    }
}
