using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using umbraco.BusinessLogic;
using umbraco;
using System.IO;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;
using umbraco.businesslogic;
using umbraco.IO;

namespace DeveloperFriendly
{
    public class DeveloperFriendlyApplication : ApplicationStartupHandler
    {
        [Flags]
        public enum SyncMode
        { 
            Disabled = 0,
            Outward = 1,
            Inward = 2,
            Both = 3
        }
        

        public DeveloperFriendlyApplication() {


            var mode = SyncMode.Both;
            if (!Enum.TryParse<SyncMode>(ConfigurationManager.AppSettings["DeveloperFriendly:Mode"], true, out mode))
                mode = SyncMode.Both;
            
            var deleteMissingTypes = false;
            if (!bool.TryParse(ConfigurationManager.AppSettings["DeveloperFriendly:DeleteMissingTypes"], out deleteMissingTypes))
                deleteMissingTypes = false;

            if (mode != SyncMode.Disabled)
            {
                var root = Path.Combine(IOHelper.MapPath("~/config"), "DeveloperFriendly");

                if (!Directory.Exists(root))
                    Directory.CreateDirectory(root);

                new DataTypeSyncer(root, mode, deleteMissingTypes);
                new MemberTypeSerializer(root, mode, deleteMissingTypes);
                new TemplateSerializer(root, mode, deleteMissingTypes);
                new MacroSerializer(root, mode, deleteMissingTypes);
                new MediaTypeSerializer(root, mode, deleteMissingTypes);
                new DocumentTypeSyncer(root, mode, deleteMissingTypes);
                new ContentSyncer(root, mode);
            }

        }

    }
}