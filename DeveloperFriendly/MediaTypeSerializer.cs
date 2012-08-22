using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using umbraco.cms.businesslogic.web;
using umbraco.BusinessLogic;
using System.IO;
using umbraco;
using System.Xml.Linq;
using umbraco.cms.businesslogic.propertytype;
using umbraco.cms.businesslogic.datatype;
using umbraco.cms.businesslogic.media;
using System.Xml;

namespace DeveloperFriendly
{
    public class MediaTypeSerializer : BaseTypeSyncer
    {

        public MediaTypeSerializer(string rootFolder, DeveloperFriendly.DeveloperFriendlyApplication.SyncMode mode, bool deleteMissingTypes)
            : base(Path.Combine(rootFolder, "MediaTypes"), mode, deleteMissingTypes)
        {

        }



        protected override void RegisterChangeEvents(Action action)
        {
            MediaType.AfterNew += (s, e) => { action(); };
            MediaType.AfterDelete += (s, e) => { action(); };
            MediaType.AfterSave += (s, e) => { action(); };

        }

        protected override void DumpConfigs()
        {
            var allDocTypes = MediaType.GetAllAsList();
            System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();

            //need to check for xml change before between file on drive and expoorted before overwriting.
            List<string> currentFiles = new List<string>();
            var di = new DirectoryInfo(storageFolder);
            var files = di.GetFiles();
            currentFiles.AddRange(files.Select(x => x.FullName));

            foreach (var dt in allDocTypes)
            {
                //var xml = ;
                var doc = XDocument.Parse(ToXml(xmlDoc, dt).OuterXml);
                var xml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n" + doc.ToString();
                var file = Path.Combine(storageFolder, dt.Alias + ".config");

                //var fileOnFileSystem = new FileInfo(file);
                if (File.Exists(file))
                {
                    if (File.ReadAllText(file) != xml)
                    {
                        File.WriteAllText(file, xml);
                    }
                }
                else
                {
                    File.WriteAllText(file, xml);
                }
                if (currentFiles.Contains(file))
                    currentFiles.Remove(file);
            }
            currentFiles.ForEach(x =>
            {
                File.Delete(x);
            });
        }

        protected override bool RefreshFromFile(string FullPath)
        {
            try
            {
                var xmlDoc = XDocument.Parse(File.ReadAllText(FullPath));
                dynamic dtXml = new umbraco.MacroEngines.DynamicXml(xmlDoc.Root);

                var docType = MediaType.GetByAlias((string)dtXml.Info.Alias);
                if (docType == null)
                    docType = MediaType.MakeNew(User.GetUser(0), (string)dtXml.Info.Name);

                docType.Alias = dtXml.Info.Alias;
                docType.IconUrl = dtXml.Info.Icon;
                docType.Thumbnail = dtXml.Info.Thumbnail;
                docType.Description = dtXml.Info.Description;
                docType.Text = dtXml.Info.Name;
                if (xmlDoc.Root.Element("Info").Element("Master") != null)
                    docType.MasterContentType = DocumentType.GetByAlias(xmlDoc.Root.Element("Info").Element("Master").Value).Id;
                //check if differs first


                UpdateTabs(xmlDoc, docType);

                //PropertyType.
                UpdateProperties(xmlDoc, docType);

                docType.Save();
                return true;
            }
            catch { }
            return false;
        }

        void UpdateTabs(XDocument xmlDoc, MediaType docType)
        {
            var tabs = docType.getVirtualTabs;
            var updatedTabs = xmlDoc.Descendants("Tabs").Descendants("Tab").Select(x => x.Element("Caption").Value);
            var currentNames = tabs.Select(x => x.Caption);
            var toDelete = tabs.Where(x => !updatedTabs.Contains(x.Caption));
            var toAdd = updatedTabs.Where(x => !currentNames.Contains(x));
            foreach (var t in toDelete)
                docType.DeleteVirtualTab(t.Id);
            foreach (var t in toAdd)
                docType.AddVirtualTab(t);
        }

        void UpdateProperties(XDocument xmlDoc, MediaType docType)
        {
            var updatedProperties = xmlDoc.Descendants("GenericProperty");
            var updatedPropertiesNames = updatedProperties.Select(x => x.Element("Alias").Value);

            var props = PropertyType.GetAll().Where(x => x.ContentTypeId == docType.Id);

            var matchingProps = props.Where(x => updatedPropertiesNames.Contains(x.Alias));

            var toDelete = props.Where(x => !matchingProps.Contains(x));
            var newProperties = updatedProperties.Where(x => !matchingProps.Select(y => y.Alias).Contains(x.Element("Alias").Value));

            foreach (var p in toDelete)
                p.delete();
            var allDtd = DataTypeDefinition.GetAll();
            var tabs = docType.getVirtualTabs;
            foreach (var p in matchingProps)
            {
                var match = updatedProperties.Where(x => x.Element("Alias").Value == p.Alias).Single();
                var dtd = allDtd.Where(x => x.UniqueId == new Guid(match.Element("Definition").Value)).Single();

                p.DataTypeDefinition = dtd;
                p.Description = match.Element("Description").Value;
                var t = tabs.Where(x => x.Caption == match.Element("Tab").Value).FirstOrDefault();
                if (t != null)
                    p.TabId = t.Id;
                p.Mandatory = bool.Parse(match.Element("Mandatory").Value);
                p.ValidationRegExp = match.Element("Validation").Value;
                p.Name = match.Element("Name").Value;
                p.Save();
            }


            foreach (var m in newProperties)
            {
                var dtd = allDtd.Where(x => x.UniqueId == new Guid(m.Element("Definition").Value)).Single();
                var p = PropertyType.MakeNew(dtd, docType, m.Element("Name").Value, m.Element("Alias").Value);

                p.Description = m.Element("Description").Value;
                p.TabId = tabs.Where(x => x.Caption == m.Element("Tab").Value).First().Id;
                p.Mandatory = bool.Parse(m.Element("Mandatory").Value);
                p.ValidationRegExp = m.Element("Validation").Value;
                p.Save();
            }
        }



        public XmlElement ToXml(XmlDocument xd, MediaType mt)
        {
            XmlElement doc = xd.CreateElement("MediaType");

            // info section
            XmlElement info = xd.CreateElement("Info");
            doc.AppendChild(info);
            info.AppendChild(xmlHelper.addTextNode(xd, "Name", mt.Text));
            info.AppendChild(xmlHelper.addTextNode(xd, "Alias", mt.Alias));
            info.AppendChild(xmlHelper.addTextNode(xd, "Icon", mt.IconUrl));
            info.AppendChild(xmlHelper.addTextNode(xd, "Thumbnail", mt.Thumbnail));
            info.AppendChild(xmlHelper.addTextNode(xd, "Description", mt.Description));

            if (mt.MasterContentType > 0)
            {
                DocumentType dt = new DocumentType(mt.MasterContentType);

                if (dt != null)
                    info.AppendChild(xmlHelper.addTextNode(xd, "Master", dt.Alias));
            }


            // structure
            XmlElement structure = xd.CreateElement("Structure");
            doc.AppendChild(structure);

            foreach (int cc in mt.AllowedChildContentTypeIDs.ToList())
                structure.AppendChild(xmlHelper.addTextNode(xd, "MediaType", new DocumentType(cc).Alias));

            // generic properties
            XmlElement pts = xd.CreateElement("GenericProperties");
            foreach (PropertyType pt in mt.PropertyTypes)
            {
                //only add properties that aren't from master doctype
                if (pt.ContentTypeId == mt.Id)
                {
                    XmlElement ptx = xd.CreateElement("GenericProperty");
                    ptx.AppendChild(xmlHelper.addTextNode(xd, "Name", pt.Name));
                    ptx.AppendChild(xmlHelper.addTextNode(xd, "Alias", pt.Alias));
                    ptx.AppendChild(xmlHelper.addTextNode(xd, "Type", pt.DataTypeDefinition.DataType.Id.ToString()));

                    //Datatype definition guid was added in v4 to enable datatype imports
                    ptx.AppendChild(xmlHelper.addTextNode(xd, "Definition", pt.DataTypeDefinition.UniqueId.ToString()));

                    ptx.AppendChild(xmlHelper.addTextNode(xd, "Tab", umbraco.cms.businesslogic.ContentType.Tab.GetCaptionById(pt.TabId)));
                    ptx.AppendChild(xmlHelper.addTextNode(xd, "Mandatory", pt.Mandatory.ToString()));
                    ptx.AppendChild(xmlHelper.addTextNode(xd, "Validation", pt.ValidationRegExp));
                    ptx.AppendChild(xmlHelper.addCDataNode(xd, "Description", pt.Description));
                    pts.AppendChild(ptx);
                }
            }
            doc.AppendChild(pts);

            // tabs
            XmlElement tabs = xd.CreateElement("Tabs");
            foreach (umbraco.cms.businesslogic.ContentType.TabI t in mt.getVirtualTabs.ToList())
            {
                //only add tabs that aren't from a master doctype
                if (t.ContentType == mt.Id)
                {
                    XmlElement tabx = xd.CreateElement("Tab");
                    tabx.AppendChild(xmlHelper.addTextNode(xd, "Id", t.Id.ToString()));
                    tabx.AppendChild(xmlHelper.addTextNode(xd, "Caption", t.Caption));
                    tabs.AppendChild(tabx);
                }
            }
            doc.AppendChild(tabs);
            return doc;
        }
        protected override Dictionary<string, string> ExpectedConfigs()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            var allTypes = MediaType.GetAllAsList();

            foreach (var t in allTypes)
            {
                dic.Add(t.Alias, Path.Combine(storageFolder, t.Alias + ".config"));
            }

            return dic;
        }

        protected override bool Delete(string alias)
        {
            try
            {
                var t = MediaType.GetByAlias(alias);
                if (t != null)
                {
                    t.delete();
                }

                return true;
            }
            catch { }
            return false;
        }

    }

}