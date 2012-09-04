using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using umbraco.cms.businesslogic.property;
using umbraco.cms.businesslogic.datatype;
using umbraco.cms.businesslogic;
using System.Xml.Linq;
using DeveloperFriendly.Extensions;

namespace DeveloperFriendly.PropertyConverters
{
    public class MultiNodeTreePickerConverter : IPropertyConverter
    {
        public int Order
        {
            get { return 0; }
        }

        public bool CanConvert(umbraco.cms.businesslogic.property.Property prop)
        {
            return (prop.PropertyType.DataTypeDefinition.DataType.DataTypeName == "Multi-Node Tree Picker");
        }

        
        public void SetProperty(umbraco.cms.businesslogic.property.Property prop, System.Xml.Linq.XElement root)
        {
            var s = new Settings(prop);

            var elm = root.Element(prop.PropertyType.Alias);
            var paths = elm.Elements("item").Select(x => x.Value);

            IEnumerable<CMSNode> nodes = null;
            if (s.Source == Settings.DataSource.Content)
            {
                nodes = paths.Select(x => ContentSyncer.Find(x));
            }
            else
            {
                nodes = paths.Select(x => MediaSyncer.Find(x));
            }

            var ids = nodes.Select(x => x.Id);
            if (s.Format == Settings.DataFormat.Csv)
            {
                prop.Value = string.Join(",", ids);
            }
            else {

                var xml = new XElement("MultiNodePicker",
                        new XAttribute("type", s.Source.ToString().ToLower())
                    );
                foreach (var id in ids) { 
                    xml.Add(new XElement("nodeId", id));
                }

                prop.Value = xml.ToString();
            }

        }

        public System.Xml.Linq.XElement GetProperty(umbraco.cms.businesslogic.property.Property prop)
        {
            var s = new Settings(prop);

            IEnumerable<CMSNode> nodes = new List<CMSNode>();
            IEnumerable<int> nodeIds = new List<int>();
            if ((string)prop.Value != "")
            {
                if (s.Format == Settings.DataFormat.Csv)
                {
                    nodeIds = prop.Value.ToString().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => int.Parse(x));
                }
                else
                {
                    
                    nodeIds = XElement.Parse(prop.Value.ToString()).Elements("nodeId").Select(x => int.Parse(x.Value));
                   
                }
            }

            nodes = nodeIds.Select(x => new CMSNode(x));

            var elm = new XElement(prop.PropertyType.Alias);
            foreach (var n in nodes) {
                elm.Add(new XElement("item", n.ConfigPath()));
            }
            return elm;
        }

        public class Settings
        {
            Property _p;
            public Settings(Property p) {
                _p = p;

                var preValues = PreValues.GetPreValues(_p.PropertyType.DataTypeDefinition.Id).Values.OfType<PreValue>().OrderBy(x=>x.SortOrder).ToList();
                Source = (preValues[0].Value == "content") ? DataSource.Content : DataSource.Media;
                Format = (preValues[4].Value == "1") ? DataFormat.Csv : DataFormat.Xml;

            }
            public DataFormat Format { get; private set; }
            public DataSource Source { get; private set; }

            public enum DataSource
            {
                Content,
                Media
            }
            public enum DataFormat
            {
                Xml,
                Csv
            }
        }
    }
}
