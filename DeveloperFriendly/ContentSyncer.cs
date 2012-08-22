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

namespace DeveloperFriendly
{
    /// <summary>
    /// this is a one time import process,
    /// this will output the content as its created but it will only every sync in when there are no content items in tree
    /// </summary>
    public class ContentSyncer : BaseTypeSyncer
    {
        public ContentSyncer(string rootFolder, DeveloperFriendly.DeveloperFriendlyApplication.SyncMode mode) :
         base(Path.Combine(rootFolder, "ContentItems"), mode, false){
        
        }
        protected override Dictionary<string, string> ExpectedConfigs()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            var docs = Document.GetRootDocuments();

            foreach (var d in docs)
            {
                var alias = umbraco.helper.SpaceCamelCasing(d.Text);
                dic.Add(alias, Path.Combine(storageFolder, alias + ".config"));
            }

            return dic;
        }

        protected override bool Delete(string alias)
        {
            try
            {
                foreach (var d in Document.GetRootDocuments().Select(x=>Tuple.Create(umbraco.helper.SpaceCamelCasing(x.Text),x)).Where(x=>x.Item1 == alias))
                {
                    d.Item2.delete();                    
                }

                return true;
            }
            catch {
            }

            return false;
        }

        protected override void RegisterChangeEvents(Action action)
        {
            umbraco.content.AfterUpdateDocumentCache += (s,e)=>
            {
                action();
            };
        }

        protected override bool RefreshFromFile(string FullPath)
        {

            //only import content configs if current root node is empty
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.Load(FullPath);
                var root = xmlDoc.SelectSingleNode("//*[@isDoc]");

                foreach (var d in ImportNode(root, -1, null))
                {
                    d.Publish(User.GetUser(0));
                }
            }
            catch {
                return false;
            }
            return true;

        }

        internal IEnumerable<Document> ImportNode(XmlNode node, int parentId, int? order)
        {
            List<Document> returnedDocs = new List<Document>();


            var nodeName = node.Attributes["nodeName"].InnerText;
            var docType = DocumentType.GetByAlias(node.Name);

            Document doc = null;
            Document[] toMatch = new Document[]{};
            if (Document.IsDocument(parentId))
            {
                toMatch = (new Document(parentId)).Children;
            }
            else 
            {
               toMatch = Document.GetRootDocuments();
            }
            
            doc = toMatch.Where(x => umbraco.helper.SpaceCamelCasing(x.Text) == umbraco.helper.SpaceCamelCasing(nodeName)).FirstOrDefault();
            
            if(doc == null)
                doc = Document.MakeNew(nodeName, docType, User.GetUser(0), parentId);

            returnedDocs.Add(doc);

            if (order.HasValue)
            {
                doc.sortOrder = order.Value;
            }


            //update the template
            var templateAlias = node.Attributes["template"].Value;
            var template = Template.GetByAlias(templateAlias);
            doc.Template = template.Id;

            //update the actual properties
            var properties = node.SelectNodes("*[not(@isDoc)]").OfType<XmlNode>();

            foreach (var prop in properties)
            {
                var p = doc.getProperty(prop.Name);
                if (p == null)
                {
                    p = doc.addProperty(doc.ContentType.PropertyTypes.Where(x => x.Alias == prop.Name).Single(), Guid.NewGuid());
                }
                var val = prop.InnerXml;

                if(val.StartsWith("<![CDATA[")){//strip off the cdata bits
                    val = val.Substring(9, val.Length - 12);
                }
                //TODO may need to do some type coercion here
                p.Value = val;
            }

            //import all child docs
            var children = node.SelectNodes("*[@isDoc]").OfType<XmlNode>();
            int childOrder = 0;
            foreach (var c in children)
            {
                childOrder++;
                returnedDocs.AddRange(ImportNode(c, doc.Id, childOrder));
            }

            return returnedDocs;

        }

        protected override void DumpConfigs()
        {
            var oldFiles = Directory.GetFiles(this.storageFolder, "*.config");
            foreach (var f in oldFiles)
            {
                File.Delete(f);
            }
            foreach (var d in Document.GetRootDocuments())
            {
                var xmlNode = umbraco.content.Instance.XmlContent.SelectSingleNode("//*[@id=" + d.Id + "]");
                if (xmlNode != null)
                {
                    XmlDocument xmldoc = new XmlDocument();
                    xmldoc.LoadXml(xmlNode.OuterXml);
                    var root = xmldoc.SelectSingleNode("//*[@isDoc]");
                    CleanUpXml(root);
                    var fileName = umbraco.helper.SpaceCamelCasing(d.Text) + ".config";
                    using (var fs = File.CreateText(Path.Combine(this.storageFolder, fileName)))
                    {
                        var xml = ToString(xmldoc, 4);

                        fs.Write(xml);
                        fs.Flush();
                        fs.Close();
                    }
                }
            }

        }
        static string[] documentAttributesToKeep = new string[]{
            "isDoc",
            "nodeName",
            "template"
        };

        private static void CleanUpXml(XmlNode node)
        {
            //convert the tmeplateId into a template alias
            var template = Template.GetTemplate(int.Parse(node.Attributes["template"].Value));
            node.Attributes["template"].Value = template.Alias;

            var allAttribsToRemove = node.Attributes.OfType<XmlAttribute>().Where(x => !documentAttributesToKeep.Contains(x.Name)).ToList();
            foreach (var a in allAttribsToRemove)
            {
                node.Attributes.Remove(a);
            }

            var children = node.SelectNodes("*[@isDoc]").OfType<XmlNode>();
            foreach (var c in children)
            {
                CleanUpXml(c);
            }
        }

        private static string ToString(System.Xml.XmlDocument doc, int indentation)
        {
            using (var sw = new System.IO.StringWriter())
            {
                using (var xw = new System.Xml.XmlTextWriter(sw))
                {
                    xw.Formatting = System.Xml.Formatting.Indented;
                    xw.Indentation = indentation;
                    doc.Save(xw);
                   // node.WriteTo(xw);
                }
                return sw.ToString();
            }
        }
    }
}
