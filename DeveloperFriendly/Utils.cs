using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace DeveloperFriendly
{
    public static class Utils
    {
        public static string ToAlias(this string str) {
            var url = umbraco.cms.helpers.url.FormatUrl(str).ToLower();
            return Regex.Replace(url, @"[^a-zA-Z0-9\-\.\/\:]{1}", "_");
        }
        public static string ToString(this System.Xml.XmlDocument doc, int indentation)
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
        public static string ToString(this XDocument doc, int indentation)
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
        public static void Save(this XDocument doc, string filename, int indentation)
        {
            using (var fs = File.Create(filename))
            {
                using (var sw = new System.IO.StreamWriter(fs))
                {
                    using (var xw = new System.Xml.XmlTextWriter(sw))
                    {
                        xw.Formatting = System.Xml.Formatting.Indented;
                        xw.Indentation = indentation;
                        doc.Save(xw);
                    }
                }
            }
        }

        public static string HashFolder(string path)
        {
            return HashFolder(path, "*");
        }

        public static string HashFolder(string path, string pattern)
        {
            var files = Directory.GetFiles(path, pattern).Select(x => Path.GetFileName(x)).OrderBy(x => x);

            StringBuilder sb = new StringBuilder();

            foreach (var f in files)
            {
                var fileName = Path.Combine(path, f);
                sb.Append(f);

                using (FileStream file = new FileStream(fileName, FileMode.Open))
                {
                    using (MD5 md5 = new MD5CryptoServiceProvider())
                    {
                        byte[] retVal = md5.ComputeHash(file);
                        file.Close();
                        sb.Append(Convert.ToBase64String(retVal));
                    }
                }
            }

            return HashString(sb.ToString());
        }
        public static string HashString(string str)
        {
            using (MD5 md5 = new MD5CryptoServiceProvider())
            {
                byte[] retVal = md5.ComputeHash(Encoding.Unicode.GetBytes(str));

                return Convert.ToBase64String(retVal);
            }
        }
    }
}