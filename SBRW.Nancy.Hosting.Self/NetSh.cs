using System;

namespace SBRW.Nancy.Hosting.Self
{
    /// <summary>
    /// Executes NetSh commands
    /// </summary>
    public static class NetSh
    {
        private static string NetshCommand
        {
            get
            {
                return "netsh";
            }
        }

        /// <summary>
        /// Add a url reservation
        /// </summary>
        /// <param name="url">Url to add</param>
        /// <param name="user">User to add the reservation for</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool AddUrlAcl(string url, string user)
        {
            try
            {
                string arguments = GetParameters(url, user);

                return UacHelper.RunElevated(NetshCommand, arguments);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static string GetParameters(string url, string user)
        {
            return string.Format("http add urlacl url=\"{0}\" user=\"{1}\"", url, user);
        }
    }
}