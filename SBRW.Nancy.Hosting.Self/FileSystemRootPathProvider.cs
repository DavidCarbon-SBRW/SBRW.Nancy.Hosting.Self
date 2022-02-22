using System;
using System.IO;
using System.Reflection;

namespace SBRW.Nancy.Hosting.Self
{
    /// <summary>
    /// 
    /// </summary>
    public class FileSystemRootPathProvider : IRootPathProvider
    {
        private Lazy<string> L_RootPath { get; set; } = new Lazy<string>(ExtractRootPath);
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string GetRootPath()
        {
            return this.L_RootPath.Value;
        }

        private static string ExtractRootPath()
        {
            return Path.GetDirectoryName((Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).Location);
        }
    }
}
