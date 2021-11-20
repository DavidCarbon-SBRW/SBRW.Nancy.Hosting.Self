using Nancy;
using System;
using System.IO;
using System.Reflection;

namespace SBRW.Nancy.Hosting.Self
{
    public class FileSystemRootPathProvider : IRootPathProvider
    {
        private readonly Lazy<string> rootPath = new Lazy<string>(ExtractRootPath);

        public string GetRootPath()
        {
            return this.rootPath.Value;
        }

        private static string ExtractRootPath()
        {
            Assembly assembly =
                Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

            string location = assembly.Location;

            return Path.GetDirectoryName(location);
        }
    }
}
