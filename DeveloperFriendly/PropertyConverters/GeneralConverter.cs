using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DeveloperFriendly.PropertyConverters
{
    public class GeneralConverter : IPropertyConverter
    {
        public int Order
        {
            get { return 9999; }
        }

        public bool CanConvert(umbraco.cms.businesslogic.property.Property prop)
        {
            return true;
        }

        public void SetProperty(umbraco.cms.businesslogic.property.Property prop, System.Xml.Linq.XElement root)
        {
            var elm = root.Element(prop.PropertyType.Alias);

            if (elm != null)
            {
                prop.Value = elm.Value;
            }
        }

        XmlDocument _tmp_doc = new XmlDocument();
        public System.Xml.Linq.XElement GetProperty(umbraco.cms.businesslogic.property.Property prop)
        {
            XmlNode myNode = prop.ToXml(_tmp_doc);
            return XDocument.Parse(myNode.OuterXml).Root;
        }
    }
}
