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
using umbraco.cms.businesslogic.media;

namespace DeveloperFriendly
{
    /// <summary>
    /// this is a one time import process,
    /// this will output the content as its created but it will only every sync in when there are no content items in tree
    /// </summary>
    
    internal class MediaSyncer : BaseTypeSyncer
    {
        public MediaSyncer(string rootFolder, DeveloperFriendly.DeveloperFriendlyApplication.SyncMode mode, bool deleteMissingTypes) :
            base(Path.Combine(rootFolder, "MediaItems"), mode, deleteMissingTypes)
        {
        
        }

        protected override Dictionary<string, string> ExpectedConfigs()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            var allMedia = GetAllMedia();
            foreach (var d in allMedia)
            {
                var fileName = d.ConfigFileName();
                dic.Add(d.Id.ToString(), Path.Combine(storageFolder, fileName));
            }

            return dic;
        }

        public IEnumerable<Media> GetAllMedia() 
        {
            var docs = Media.GetRootMedias();
            return docs.Union(docs.SelectMany(x => x.GetDescendants().OfType<Media>()));
        }


        protected override bool Delete(string Id)
        {
            try
            {
                var id = int.Parse(Id);
                var doc = new Media(id);
                
                doc.delete();
                
                return true;
            }
            catch {
            }

            return false;
        }


        private void EnsureUniqueName(CMSNode m, Action callback)
        {
            if (m.IsDuplicateName())
            {
                //if more than 1 we need to resave with a new name
                var i = 1;

                //find then next available no dup name                    
                do
                {
                    m.Text = m.Text + " - " + i;
                    i++;
                } while (m.IsDuplicateName());


                // this will indirectly make AfterSave be called which will cause the media to sync
                m.Save();
            }
            else
            {
                // only invoke exporting and dumping if not having the change the name
                callback();
            }
        }

        protected override void RegisterChangeEvents(Action action)
        {
            
            Media.AfterDelete += (s, e) =>
            {
                action();
            };
           
            Media.AfterSave += (s, e) =>
            {
                EnsureUniqueName((CMSNode)s, action);                
            };
            Media.AfterNew += (s, e) =>
            {
                EnsureUniqueName((CMSNode)s, action);
            };            
            Media.AfterMove += (s, e) =>
            {
                action();
            };
        }

        protected override bool RefreshFromFile(string FullPath)
        {
            try
            {
                var xmlString = File.ReadAllText(FullPath);
                XDocument xml = XDocument.Parse(xmlString);

                var root = xml.Root;
                //DocumentType="Page" 
                //Name="content Page" 
                //Template="Page" 
                //Path="/home"

                var docType = root.Attribute("Type").Value;
                var name = root.Attribute("Name").Value;
                var path = root.Attribute("Path").Value;
                
                var doc = FindOrGet(path, name, docType);

                

                var dt = MediaType.GetByAlias(docType);
                doc.ContentType = dt;



                var allProps = doc.GenericProperties;

                var convert = new UploadFieldConverter();//us this so when its loaded in later we use the same test
                var uploads = allProps.Where(x => convert.CanConvert(x));
                var otherProps = allProps.Where(x => !convert.CanConvert(x));

                //update file paths first then go and update other properties because upload field goes and auto updates other properties                
                foreach(var p in uploads){
                    SetProperty(p, root);
                };
                foreach(var p in otherProps){
                    SetProperty(p, root);
                };

                doc.Save();

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

        public static Media Find(string path)
        {
            IEnumerable<string> parts = path.Split('/').Skip(1);
            Media item = null;
            IEnumerable<Media> toCheck = Media.GetRootMedias();
            while (parts.Count() > 0)
            {
                item = toCheck.Where(x => x.Text.ToAlias() == parts.First()).FirstOrDefault();
                parts = parts.Skip(1);
                if (item == null)
                {
                    return null;
                }
            }
            return item;
        }


        private Media FindOrGet(string path, string name, string docType)
        {

            var parent = Find(path);
            var parentId = -1;
            IEnumerable<Media> toCheck = null;
            if (parent != null || path == "/")
            {
                if (parent != null)
                {
                    toCheck = parent.Children;
                    parentId = parent.Id;
                }
                else
                {
                    toCheck = Media.GetRootMedias();
                }
                //we should now have the parent doc by now

                var doc = toCheck.Where(x => x.Text.ToAlias() == name.ToAlias()).FirstOrDefault();

                if (doc == null)
                {
                    var dt = MediaType.GetByAlias(docType);
                    doc = Media.MakeNew(name, dt, new User(0), parentId);
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
            foreach (var d in GetAllMedia())
            {
                var fileName = d.ConfigFileName();
                var path = Path.Combine(this.storageFolder, fileName);
                try
                {
                    using (var fs = File.CreateText(path))
                    {
                        var xml = ToXml(d);

                        fs.Write(xml);
                        fs.Flush();
                        fs.Close();
                    }
                }
                catch {
                    if(File.Exists(path))
                        File.Delete(path);
                }
            }
        }

        public string ToXml(Media doc)
        {             
            var parentPath = "/";
            if (doc.ParentId > 0)
                parentPath = doc.Parent.ConfigPath();
            
         
            var elm = new XElement("Content",
                    new XAttribute("Type", doc.ContentType.Alias),
                    new XAttribute("Name", doc.Text),
                    new XAttribute("Path", parentPath)
                    );

            var tmpXmlDoc  = new XmlDocument();
            foreach(var p in doc.GenericProperties)
            {
                elm.Add(GetProperty(p));
            }
            XDocument xml = new XDocument(elm);

            return xml.ToString(4);
        }

    }
}
