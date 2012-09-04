using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using umbraco.cms.businesslogic;
using umbraco.cms.businesslogic.web;
using umbraco.cms.businesslogic.media;
using umbraco.BusinessLogic.console;

namespace DeveloperFriendly.Extensions
{
    public static class CMSNodeExtensions
    {
        public static string ConfigFileName(this CMSNode doc, bool includeExtension = true)
        {
            var url = "";

            if (doc.ParentId > 0)
            {
                url = ConfigFileName(doc.Parent, false) + ".";
            }

            url += doc.Text.ToAlias();

            if (includeExtension)
                return url + ".config";
            else
                return url;
        }

        public static bool IsDuplicateName(this CMSNode node)
        {
            IEnumerable<IconI> siblings = null;
            if (node.ParentId < 0)
            { 
                if(Document.IsDocument(node.Id))
                    siblings  = Document.GetRootDocuments();
                else
                    siblings  = Media.GetRootMedias();

            }else{
                siblings = node.Parent.Children;
            }
            //remove self
            siblings = siblings.Where(x => x.Id != node.Id);

            return siblings.Where(x=>x.Text.ToAlias() == node.Text.ToAlias()).Any();
        }

        public static string ConfigPath(this CMSNode doc)
        {
            var url = "";

            if (doc.ParentId > 0)
            {
                url = ConfigPath(doc.Parent);
            }

            url += "/" + doc.Text.ToAlias();

            return url;
        }
    }
}
