using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic.media;
using DeveloperFriendly.Extensions;
using umbraco.cms.businesslogic.web;

namespace DeveloperFriendly.PropertyConverters
{
    public class ContentPickerConverter : IPropertyConverter
    {
        public int Order
        {
            get { return 0; }
        }

        public bool CanConvert(umbraco.cms.businesslogic.property.Property prop)
        {
            return (prop.PropertyType.DataTypeDefinition.DataType.DataTypeName == "Content Picker");
        }

        public void SetProperty(umbraco.cms.businesslogic.property.Property prop, System.Xml.Linq.XElement root)
        {         
            var elm = root.Element(prop.PropertyType.Alias);
            
            if (elm != null)
            {
                var itm = ContentSyncer.Find(elm.Value);

                if (itm == null)
                    prop.Value = elm.Value;
                else
                    prop.Value = itm.Id;
            }
        }

        XmlDocument _tmp_doc = new XmlDocument();
        public System.Xml.Linq.XElement GetProperty(umbraco.cms.businesslogic.property.Property prop)
        {
            //get access to media item based on some path.
            var itm = new Document(int.Parse(prop.Value.ToString()));
            
            return new XElement(prop.PropertyType.Alias, itm.ConfigPath());
        }
    }
}
