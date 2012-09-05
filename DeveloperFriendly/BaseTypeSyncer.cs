using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using System.Xml.Linq;

namespace DeveloperFriendly
{
    public abstract class BaseTypeSyncer
    {
        public void ExportAll()
        {
            if (watcher != null)
                watcher.EnableRaisingEvents = false;

            if (Directory.Exists(storageFolder))
            {
                Directory.Delete(storageFolder, true);
            }
            Directory.CreateDirectory(storageFolder);

            DumpConfigFiles();


            if (watcher != null)
                watcher.EnableRaisingEvents = true;

        }

        public void ImportAll()
        {
            ImportAll(_deleteMissingTypes);
        }

        public void StartWatching()
        {
            //only start if needed
            if ((_mode & DeveloperFriendlyApplication.SyncMode.Inward) == DeveloperFriendlyApplication.SyncMode.Inward && watcher == null)
            {
                watcher = new FileSystemWatcher(storageFolder);

                watcher.Created += new FileSystemEventHandler(watcher_Changed);
                watcher.Changed += new FileSystemEventHandler(watcher_Changed);
                watcher.Deleted += new FileSystemEventHandler(watcher_Changed);
                watcher.EnableRaisingEvents = true;
            }
        }


        protected string storageFolder;
        string hashFile;
        FileSystemWatcher watcher;
        bool _deleteMissingTypes;
        DeveloperFriendly.DeveloperFriendlyApplication.SyncMode _mode;
        public BaseTypeSyncer(string folder, DeveloperFriendly.DeveloperFriendlyApplication.SyncMode mode, bool deleteMissingTypes)
         {
             _mode = mode;
             storageFolder = folder;
             hashFile = Path.Combine(folder, ".hash");
            
            _deleteMissingTypes = deleteMissingTypes;

             bool requiresFirstDump = false;
             if (!Directory.Exists(storageFolder))
             {
                 Directory.CreateDirectory(storageFolder);
                 requiresFirstDump = true;
             }


             if ((mode & DeveloperFriendlyApplication.SyncMode.Inward) == DeveloperFriendlyApplication.SyncMode.Inward)
             {
                 ImportAll(deleteMissingTypes && !requiresFirstDump);
             }

             if ((mode & DeveloperFriendlyApplication.SyncMode.Outward) == DeveloperFriendlyApplication.SyncMode.Outward)
             {

                 RegisterChangeEvents(() => {
                     DumpConfigFiles();
                 });

                 if (requiresFirstDump)
                     DumpConfigFiles();
             }


         }

        private void ImportAll(bool doDelete)
        {
            if (watcher != null)
                watcher.EnableRaisingEvents = false;

            if (doDelete)
            {
                var configs = ExpectedConfigs();

                var toDelete = configs.Where(x => !File.Exists(x.Value)).Select(x => x.Key).ToList();

                var sucesscount = 0;
                var tries = 0;

                while (sucesscount < toDelete.Count && tries < 10)
                {
                    sucesscount = 0;
                    toDelete.ForEach(x =>
                    {
                        if (Delete(x))
                            sucesscount++;
                    });
                    tries++;
                }

            }
            string fileHash = "";
            if (IsHashWrong(out fileHash))
            {
                var docs = LoadDocuments().ToList();
                var tries = 0;

                while (docs.Count > 0  && tries < 10)
                {
                    foreach(var d in docs.ToArray()){
                        if (RefreshFromXml(d))
                        {
                            docs.Remove(d);
                        }
                    }

                    tries++;
                }
                File.WriteAllText(hashFile, fileHash);
            }

            if (watcher != null)
                watcher.EnableRaisingEvents = true;
        }
                

        /// <summary>
        /// alias -> filename par
        /// </summary>
        /// <returns></returns>
        protected abstract Dictionary<string, string> ExpectedConfigs();
        protected abstract bool Delete(string alias);

        protected abstract void RegisterChangeEvents(Action action);

        protected abstract bool RefreshFromXml(XDocument doc);
        protected abstract IEnumerable<XDocument> LoadDocuments();
        


        protected abstract void DumpConfigs();

        private void DumpConfigFiles()
        {
            if (watcher != null)
                watcher.EnableRaisingEvents = false;

            DumpConfigs();

            //write out the has file back to file system as this is the current DB state
            var fileHash = Utils.HashFolder(storageFolder, "*.config");
            File.WriteAllText(hashFile, fileHash);

            if (watcher != null)
                watcher.EnableRaisingEvents = true;

        }
        
        void watcher_Changed(object sender, FileSystemEventArgs e)
        {
            //ImportAll();
            HttpRuntime.UnloadAppDomain();
        }


        protected bool IsHashWrong()
        {
            string hash = "";
            return IsHashWrong(out hash);
        }
        protected bool IsHashWrong(out string hash)
        {
            hash = Utils.HashFolder(storageFolder, "*.config");

            if (File.Exists(hashFile))
            {
                var oldHash = File.ReadAllText(hashFile);
                return hash != oldHash;
            
            }

            return true;
        }

       
    }
}