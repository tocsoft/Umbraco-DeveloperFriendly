using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using umbraco.cms.businesslogic.media;
using DeveloperFriendly.Extensions;
using umbraco.cms.businesslogic.web;
using System.Web;
using System.IO;
using umbraco.IO;

namespace DeveloperFriendly.PropertyConverters
{
    public class UploadFieldConverter : IPropertyConverter
    {
        public int Order
        {
            get { return 0; }
        }

        public bool CanConvert(umbraco.cms.businesslogic.property.Property prop)
        {
            return (prop.PropertyType.DataTypeDefinition.DataType.DataTypeName == "Upload field");
        }

        public void SetProperty(umbraco.cms.businesslogic.property.Property prop, System.Xml.Linq.XElement root)
        {
            var elm = root.Element(prop.PropertyType.Alias);

            if (elm != null)
            {
                var file = new File(elm.Value);
                if (file.Info.Exists)
                    prop.Value = file;
                else
                    prop.Value = elm.Value;
            }
        }

        XmlDocument _tmp_doc = new XmlDocument();
        public System.Xml.Linq.XElement GetProperty(umbraco.cms.businesslogic.property.Property prop)
        {
            XmlNode myNode = prop.ToXml(_tmp_doc);
            return XDocument.Parse(myNode.OuterXml).Root;
        }

        public class File : HttpPostedFileBase {
            FileInfo _info;
            Lazy<Stream> _fileStream;
            public File(string path) { 
                _info = new FileInfo(IOHelper.MapPath(path));
                _fileStream = new Lazy<Stream>(() => _info.OpenRead());
            }
            public FileInfo Info { get { return _info; } }
            public override System.IO.Stream InputStream
            {
                get
                {
                    return _fileStream.Value;
                }
            }

            public override int ContentLength
            {
                get
                {
                    return (int)_info.Length;
                }
            }
            public override string FileName
            {
                get
                {
                    return _info.Name;
                }
            }
        }
    }
}
