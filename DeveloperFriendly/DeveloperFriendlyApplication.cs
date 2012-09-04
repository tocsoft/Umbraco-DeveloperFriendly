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

        internal static List<BaseTypeSyncer> _syncers = new List<BaseTypeSyncer>();

        public static void ExportAllConfigs()
        {
            foreach (var s in _syncers)
            {
                s.ExportAll();
            }
        }

        public static void ImportAllConfigs()
        {
            foreach (var s in _syncers)
            {
                s.ImportAll();
            }
        }

        public DeveloperFriendlyApplication() {

            var installer = Application.SqlHelper.Utility.CreateInstaller();
            if (!installer.IsEmpty)
            {

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

                    _syncers.Add(new DataTypeSyncer(root, mode, deleteMissingTypes));
                    _syncers.Add(new MemberTypeSerializer(root, mode, deleteMissingTypes));
                    _syncers.Add(new TemplateSerializer(root, mode, deleteMissingTypes));
                    _syncers.Add(new MacroSerializer(root, mode, deleteMissingTypes));
                    _syncers.Add(new MediaTypeSerializer(root, mode, deleteMissingTypes));
                    _syncers.Add(new DocumentTypeSyncer(root, mode, deleteMissingTypes));

                    //sync media first as its more likely going to be a dependency on content then the other way around
                    _syncers.Add(new MediaSyncer(root, mode, deleteMissingTypes));
                    _syncers.Add(new ContentSyncer(root, mode, deleteMissingTypes));
                }
            }

        }

    }
}