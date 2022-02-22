using System;
using System.Collections.Generic;
using System.Text;

namespace SBRW.Nancy.Hosting.Self
{
    /// <summary>
    /// Exception for when automatic address reservation creation fails.
    /// Provides the user with manual instructions.
    /// </summary>
    public class AutomaticUrlReservationCreationFailureException : Exception
    {
        private IEnumerable<string> E_Prefixes { get; set; }
        private string E_User { get; set; }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="X_Prefixes"></param>
        /// <param name="X_User"></param>
        public AutomaticUrlReservationCreationFailureException(IEnumerable<string> X_Prefixes, string X_User)
        {
            this.E_Prefixes = X_Prefixes;
            this.E_User = X_User;
        }

        /// <summary>
        /// Gets a message that describes the current exception.
        /// </summary>
        /// <returns>
        /// The error message that explains the reason for the exception, or an empty string("").
        /// </returns>
        /// <filterpriority>1</filterpriority>
        public override string Message
        {
            get
            {
                StringBuilder stringBuilder = new StringBuilder();

                stringBuilder.AppendLine("The Nancy self host was unable to start, as no namespace reservation existed for the provided url(s).");
                stringBuilder.AppendLine();

                stringBuilder.AppendLine("Please either enable UrlReservations.CreateAutomatically on the HostConfiguration provided to ");
                stringBuilder.AppendLine("the NancyHost, or create the reservations manually with the (elevated) command(s):");
                stringBuilder.AppendLine();

                foreach (var prefix in E_Prefixes)
                {
                    string command = NetSh.GetParameters(prefix, E_User);
                    stringBuilder.AppendLine(string.Format("netsh {0}", command));
                }

                return stringBuilder.ToString();
            }
        }
    }
}
