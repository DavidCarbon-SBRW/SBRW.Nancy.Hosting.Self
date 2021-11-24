﻿using System;
using System.Security.Principal;

namespace SBRW.Nancy.Hosting.Self
{
    /// <summary>
    /// Configuration for automatic url reservation creation
    /// </summary>
    public class UrlReservations
    {
        private const string EveryoneAccountName = "Everyone";

        private static readonly IdentityReference EveryoneReference =
            new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        /// <summary>
        /// 
        /// </summary>
        public UrlReservations()
        {
            this.CreateAutomatically = false;
            this.User = GetEveryoneAccountName();
        }

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

        private static string GetEveryoneAccountName()
        {
            try
            {
                NTAccount account = EveryoneReference.Translate(typeof(NTAccount)) as NTAccount;
                if (account != null)
                {
                    return account.Value;
                }

                return EveryoneAccountName;
            }
            catch (Exception)
            {
                return EveryoneAccountName;
            }
        }
    }
}
