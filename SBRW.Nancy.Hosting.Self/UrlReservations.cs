using System;
using System.Security.Principal;

namespace SBRW.Nancy.Hosting.Self
{
    /// <summary>
    /// Configuration for automatic url reservation creation
    /// </summary>
    public class UrlReservations
    {
        /// <summary>
        /// 
        /// </summary>
        private string EveryoneAccountName 
        { 
            get 
            { 
                return "Everyone"; 
            } 
        }

#if NETFRAMEWORK || NETSTANDARD2_0 || NET5_0_OR_GREATER && WINDOWS
        /// <summary>
        /// 
        /// </summary>
        private IdentityReference EveryoneReference 
        { 
            get 
            {
                if (Environment.OSVersion.Platform != PlatformID.Unix)
                {
                    return new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                }
                else
                {
                    return null;
                }
            } 
        }
#endif

        /// <summary>
        /// Gets or sets a value indicating whether url reservations
        /// are automatically created when necessary.
        /// Defaults to false.
        /// </summary>
        public bool CreateAutomatically { get; set; }

        /// <summary>
        /// Gets or sets a value for the user to use to create the url reservations for.
        /// Defaults to the "Everyone" group.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private string GetEveryoneAccountName()
        {
            try
            {
#if NETFRAMEWORK || NETSTANDARD2_0 || NET5_0_OR_GREATER && WINDOWS
                if (Environment.OSVersion.Platform != PlatformID.Unix)
                {
                    NTAccount account = EveryoneReference.Translate(typeof(NTAccount)) as NTAccount;
                    if (account != null)
                    {
                        return account.Value;
                    }
                }
#endif

                return EveryoneAccountName;
            }
            catch (Exception)
            {
                return EveryoneAccountName;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public UrlReservations()
        {
            this.CreateAutomatically = false;
            this.User = GetEveryoneAccountName();
        }
    }
}
