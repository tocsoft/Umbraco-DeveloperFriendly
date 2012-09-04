using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using umbraco.cms.businesslogic.property;
using System.Xml.Linq;

namespace DeveloperFriendly.PropertyConverters
{
    public interface IPropertyConverter
    {
        int Order { get; }
        bool CanConvert(Property prop);
        void SetProperty(Property prop, XElement root);
        XElement GetProperty(Property prop);
    }
}
