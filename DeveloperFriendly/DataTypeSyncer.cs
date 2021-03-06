﻿using System;
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
using System.Xml;

namespace DeveloperFriendly
{
    public class DataTypeSyncer : BaseTypeSyncer
    {

        public DataTypeSyncer(string rootFolder, DeveloperFriendly.DeveloperFriendlyApplication.SyncMode mode, bool deleteMissingTypes)
            : base(Path.Combine(rootFolder, "DataTypes"), mode, false/*don't delete types from DB not in filesystem*/)
        {


        }

        protected override void RegisterChangeEvents(Action action)
        {
            //due to the fact that prevalue updated doesn't cause After save to kick-in
            //then we are going to dump datatypes when ever we save a DocType.
            DocumentType.AfterSave += (s, e) => {
                action();
            };

            DataTypeDefinition.AfterDelete += (s, e) =>
            {
                action();
            };
            DataTypeDefinition.AfterSave += (s, e) =>
            {
                action();
            };

        }

        protected override IEnumerable<XDocument> LoadDocuments()
        {
            return Directory.GetFiles(this.storageFolder, "*.config")
                .Select(x => XDocument.Parse(File.ReadAllText(x)));
        
        }
        protected override bool RefreshFromXml(XDocument xmlDoc)
        {
            try
            {
                dynamic dtXml = new umbraco.MacroEngines.DynamicXml(xmlDoc.Root);
                var node = xmlDoc.Element("DataType");
                var name = node.Attribute("Name").Value;
                var alias = name.ToAlias();
                var dataTypeId = new Guid(node.Attribute("Id").Value);

                var dataType = DataTypeDefinition.GetAll().Where(x=>x.Text.ToAlias() == alias).FirstOrDefault();
              
                if (dataType == null)
                {
                    dataType = DataTypeDefinition.MakeNew(User.GetUser(0), name);
                }
                var dtFactory = new umbraco.cms.businesslogic.datatype.controls.Factory();
                var dt = dtFactory.DataType(dataTypeId);
                dataType.DataType = dt;

                List<string> newValues = node.Element("PreValues").Elements("PreValue").Select(x => x.Attribute("Value").Value).ToList();
                List<PreValue> toKeep = new List<PreValue>();

                var preValues = PreValues.GetPreValues(dataType.Id).Values.OfType<PreValue>();
                var sortOrder = 0;
                foreach(var nv in newValues)
                {
                    sortOrder ++;
                    var pv = preValues.Where(x=>x.Value == nv).FirstOrDefault();
                    if(pv == null){
                        pv = PreValue.MakeNew(dataType.Id, nv);
                    }
                    pv.SortOrder = sortOrder;
                    toKeep.Add(pv);
                }

                var toDelete = preValues.Where(x=>!toKeep.Contains(x));

                foreach(var pv in toDelete)
                {
                    pv.Delete();
                }

                foreach(var pv in toKeep)
                {
                    pv.Save();
                }
                
                dataType.Save();

                return true;
            }
            catch(Exception ex)
            {

            }
            return false;
        }

        protected override void DumpConfigs()
        {
            try
            {
                var allTypes = DataTypeDefinition.GetAll();
                System.Xml.XmlDocument xmlDoc = new System.Xml.XmlDocument();

                var di = new DirectoryInfo(storageFolder);
                di.GetFiles().ToList().ForEach(x =>
                {
                    File.Delete(x.FullName);
                });

                foreach (var dt in allTypes)
                {
                    var doc = new XmlDocument();
                    doc.LoadXml(dt.ToXml(xmlDoc).OuterXml);
                    var xml = doc.ToString(4);

                    var fileName = dt.Text.ToAlias() + ".config";
                    var file = Path.Combine(storageFolder, fileName);
                    using (var fs = File.CreateText(file))
                    {
                        fs.Write(xml);
                    }
                }
            }
            catch { }
        }


        protected override Dictionary<string, string> ExpectedConfigs()
        {
            Dictionary<string, string> dic = new Dictionary<string, string>();

            var allTypes = DataTypeDefinition.GetAll();

            foreach (var dt in allTypes)
            {
                var alias = dt.Text.ToAlias();
                dic.Add(alias, Path.Combine(storageFolder, alias + ".config"));
            }

            return dic;
        }

        protected override bool Delete(string alias)
        {
            try
            {
                var t = DataTypeDefinition.GetAll().Where(x=>x.Text.ToAlias() == alias).FirstOrDefault();
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

