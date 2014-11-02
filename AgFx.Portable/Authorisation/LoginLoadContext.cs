using System;

namespace AgFx.Authorisation
{
    public class LoginLoadContext : LoadContext
    {
        private const string DefaultIdentity = "_Current_User_";

        public LoginLoadContext()
            : base(DefaultIdentity)
        {
        }

        public string Login { get; set; }
        public string Password { get; set; }

        /// <summary>
        /// Return true if a login attempt shoudl be made.
        /// </summary>
        public virtual bool CanAttemptLogin
        {
            get { return !String.IsNullOrEmpty(Login) && !String.IsNullOrEmpty(Password); }
        }
    }
}
