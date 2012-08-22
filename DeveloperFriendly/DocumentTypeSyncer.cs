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

namespace DeveloperFriendly
{
    public class DocumentTypeSyncer : BaseTypeSyncer
    {

        public DocumentTypeSyncer(string rootFolder, DeveloperFriendly.DeveloperFriendlyApplication.SyncMode mode, bool deleteMissingTypes)
            : base(Path.Combine(rootFolder, "DocumentTypes"), mode, deleteMissingTypes)
        {


        }

        protected override void RegisterChangeEvents(Action action)
        {
            DocumentType.AfterDelete += (s, e) =>
            {
                action();
            };
            DocumentType.AfterSave += (s, e) =>
            {
                action();
            };
            DocumentType.AfterNew += (s, e) =>
            {
                action();
            };
        }


        protected override bool RefreshFromFile(string FullPath)
        {
            try
            {
                var xmlDoc = XDocument.Parse(File.ReadAllText(FullPath));
                dynamic dtXml = new umbraco.MacroEngines.DynamicXml(xmlDoc.Root);

                var docType = DocumentType.GetByAlias((string)dtXml.Info.Alias);
                if (docType == null)
                {
                    docType = DocumentType.MakeNew(User.GetUser(0), (string)dtXml.Info.Name);

                }
                docType.Alias = dtXml.Info.Alias;
                docType.IconUrl = dtXml.Info.Icon;
                docType.Thumbnail = dtXml.Info.Thumbnail;
                docType.Description = dtXml.Info.Description;
                docType.Text = dtXml.Info.Name;
                if (xmlDoc.Root.Element("Info").Element("Master") != null)
                    docType.MasterContentType = DocumentType.GetByAlias(xmlDoc.Root.Element("Info").Element("Master").Value).Id;

                //check if differs first
                var allowedTemp = docType.allowedTemplates.Select(x => x.Alias).ToList();
                var newTemp = new List<string>();
                bool hasNew = false;

                foreach (var t in xmlDoc.Descendants("Template").Select(x => x.Value))
                {
                    newTemp.Add(t);
                    if (allowedTemp.Contains(t))
                        allowedTemp.Remove(t);
                    else
                    {
                        hasNew = true;
                    }
                }

                var allTemps = umbraco.cms.businesslogic.template.Template.GetAllAsList();
                if (hasNew || allowedTemp.Any())
                {


                    docType.allowedTemplates = allTemps.Where(x => newTemp.Contains(x.Alias) || newTemp.Contains(x.Text)).ToArray();
                }

                if (xmlDoc.Root.Element("Info").Element("DefaultTemplate") != null)
                {
                    var temp = allTemps.Where(x => dtXml.Info.DefaultTemplate == x.Alias || dtXml.Info.DefaultTemplate == x.Text).FirstOrDefault();
                    if (temp != null)
                        docType.DefaultTemplate = temp.Id;
                }
                UpdateTabs(xmlDoc, docType);

                UpdateProperties(xmlDoc, docType);

                docType.Save();

                docType.AllowedChildContentTypeIDs = xmlDoc.Root.Element("Structure").Elements("DocumentType").Select(x => DocumentType.GetByAlias(x.Value).Id).ToArray();

                docType.Save();

                return true;
            }
            catch
            {

            }
            return false;
        }

        void UpdateTabs(XDocument xmlDoc, DocumentType docType)
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

        void UpdateProperties(XDocument xmlDoc, DocumentType docType)
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


        protected override void DumpConfigs()
        {
            var allDocTypes = DocumentType.GetAllAsList();
            System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();

            //need to check for xml change before between file on drive and expoorted before overwriting.
            List<string> currentFiles = new List<string>();
            var di = new DirectoryInfo(storageFolder);
            var files = di.GetFiles();
            currentFiles.AddRange(files.Select(x => x.FullName));

            foreach (var dt in allDocTypes)
            {
                var doc = XDocument.Parse(dt.ToXml(xmlDoc).OuterXml);
                var xml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n" + doc.ToString();
                var file = Path.Combine(storageFolder, dt.Alias + ".config");

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


        protected override Dictionary<string, string> ExpectedConfigs()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            var allTypes = DocumentType.GetAllAsList();

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
                var t = DocumentType.GetByAlias(alias);
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

