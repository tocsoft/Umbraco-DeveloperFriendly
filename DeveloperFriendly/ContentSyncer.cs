using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using umbraco.cms.businesslogic.web;
using umbraco.cms.businesslogic;
using System.Xml;
using System.Xml.Linq;
using umbraco.BusinessLogic;
using umbraco.cms.businesslogic.template;
using umbraco.cms.businesslogic.property;
using umbraco.BusinessLogic.Utils;
using DeveloperFriendly.PropertyConverters;
using DeveloperFriendly.Extensions;

namespace DeveloperFriendly
{
    /// <summary>
    /// this is a one time import process,
    /// this will output the content as its created but it will only every sync in when there are no content items in tree
    /// </summary>
    
    internal class ContentSyncer : BaseTypeSyncer
    {
        public ContentSyncer(string rootFolder, DeveloperFriendly.DeveloperFriendlyApplication.SyncMode mode, bool deleteMissingTypes) :
            base(Path.Combine(rootFolder, "ContentItems"), mode, deleteMissingTypes)
        {
        
        }

        protected override Dictionary<string, string> ExpectedConfigs()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            var allDocs = GetAllDocuments();
            foreach (var d in allDocs)
            {
                var fileName = d.ConfigFileName();
                dic.Add(d.Id.ToString(), Path.Combine(storageFolder, fileName));
            }

            return dic;
        }
        
        public IEnumerable<Document> GetAllDocuments() 
        {
            var docs = Document.GetRootDocuments();
            return docs.Union(docs.SelectMany(x => x.GetDescendants().OfType<Document>()));
        }

        

        protected override bool Delete(string Id)
        {
            try
            {
                var id = int.Parse(Id);
                var doc = new Document(id);
                
                doc.delete();
                
                return true;
            }
            catch {
            }

            return false;
        }

        protected override void RegisterChangeEvents(Action action)
        {
            Document.AfterDelete += (s, e) =>
            {
                action();
            };
            Document.AfterSave += (s, e) =>
            {
                action();
            };
            Document.AfterNew += (s, e) =>
            {
                action();
            };
            Document.AfterCopy += (s, e) =>
            {
                action();
            };
            Document.AfterMove += (s, e) =>
            {
                action();
            };
        }
        protected override IEnumerable<XDocument> LoadDocuments()
        {
            var docs =  Directory.GetFiles(this.storageFolder, "*.config")
                .Select(x => XDocument.Parse(File.ReadAllText(x)))
                .OrderBy(x => x.Root.Attribute("Path").Value.Split('/').Count());
            return docs;
        }
        protected override bool RefreshFromXml(XDocument xml)
        {
            try
            {
                var root = xml.Root;
                //DocumentType="Page" 
                //Name="content Page" 
                //Template="Page" 
                //Path="/home"

                var docType = root.Attribute("Type").Value;
                var name = root.Attribute("Name").Value;
                var template = root.Attribute("Template").Value;
                var path = root.Attribute("Path").Value;
                var doc = FindOrGet(path, name, docType);


                var dt = DocumentType.GetByAlias(docType);
                doc.ContentType = dt;

                doc.GenericProperties.ForEach(p => {
                    SetProperty(p, root);
                });

                doc.Publish(new User(0));

            }
            catch 
            {
                return false;
            }
            return true;
        }

        Lazy<IEnumerable<IPropertyConverter>> _converters = new Lazy<IEnumerable<IPropertyConverter>>(() => {
            return TypeFinder.FindClassesOfType<IPropertyConverter>()
                                .Select(x => (IPropertyConverter)x.GetConstructor(new Type[] { })
                                    .Invoke(new object[] { }))
                                    .OrderBy(x=>x.Order)
                                    .ToList();
        });

        private IPropertyConverter GetConverter(Property p)
        {
            return _converters.Value.Where(x => x.CanConvert(p)).First();
        }
           

        private XElement GetProperty(Property p)
        {
            return GetConverter(p).GetProperty(p);
        }

        private void SetProperty(Property p, XElement root)
        {
            GetConverter(p).SetProperty(p, root);           
        }

        public static Document Find(string path)
        {
            if (path == "/")
                return null;

            IEnumerable<string> parts = path.Split('/').Skip(1);
            Document item = null;
            IEnumerable<Document> toCheck = Document.GetRootDocuments();
            while (parts.Count() > 0)
            {
                item = toCheck.Where(x => x.Text.ToAlias() == parts.First()).FirstOrDefault();
                toCheck = item.Children;
                parts = parts.Skip(1);
                if (item == null)
                {
                    return null;
                }
            }
            return item;
        }


        private Document FindOrGet(string path, string name, string docType)
        {
            var parent = Find(path);
            var parentId = -1;
            IEnumerable<Document> toCheck = null;
            if (parent != null || path == "/")
            {
                if (parent != null)
                {
                    toCheck = parent.Children;
                    parentId = parent.Id;
                }
                else {
                    toCheck = Document.GetRootDocuments();
                }
                //we should now have the parent doc by now

                var doc = toCheck.Where(x => x.Text.ToAlias() == name.ToAlias()).FirstOrDefault();

                if (doc == null)
                {
                    var dt = DocumentType.GetByAlias(docType);
                    doc = Document.MakeNew(name, dt, new User(0), parentId);
                }

                return doc;
            }
            return null;
        }
        
        protected override void DumpConfigs()
        {
            var oldFiles = Directory.GetFiles(this.storageFolder, "*.config");
            foreach (var f in oldFiles)
            {
                File.Delete(f);
            }
            foreach (var d in GetAllDocuments())
            {
                var fileName = d.ConfigFileName();

                Save(d, Path.Combine(this.storageFolder, fileName));
            }
        }

        public void Save(Document doc, string filename)
        {             
            var parentPath = "/";
            if (doc.ParentId > 0)
                parentPath = doc.Parent.ConfigPath();
            
            var templateAlias = "";
            var tmp = Template.GetTemplate(doc.Template);
            if(tmp != null)
            {
                templateAlias = tmp.Alias;
            }
            var elm = new XElement("Content",
                    new XAttribute("Type", doc.ContentType.Alias),
                    new XAttribute("Name", doc.Text),
                    new XAttribute("Template", templateAlias),
                    new XAttribute("Path", parentPath)
                    );

            var tmpXmlDoc  = new XmlDocument();
            foreach(var p in doc.GenericProperties)
            {
                elm.Add(GetProperty(p));
            }
            XDocument xml = new XDocument(elm);

            xml.Save(filename, 4);
        }

    }
}
