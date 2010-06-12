using System;
using System.Collections.Generic;
using System.Text;
using System.Management;
using System.Net;
using TradeLink.Common;

namespace TradeLink.AppKit
{
    /// <summary>
    /// authenticate username/url/key to 'allow list' url
    /// </summary>
    public sealed class Auth 
    {
        /// <summary>
        /// default name for authorization file
        /// </summary>
        public const string AuthFile = "\\_AuthInfo.txt";
        /// <summary>
        /// authenicate using given url
        /// </summary>
        /// <param name="authurl"></param>
        public Auth(string authurl) : this(authurl, string.Empty, string.Empty,0) { }
        /// <summary>
        /// authenticate using url and login via http to said url, look for identification in default location for program
        /// </summary>
        /// <param name="authurl"></param>
        /// <param name="useHttpAuth"></param>
        /// <param name="program"></param>
        public Auth(string authurl, bool useHttpAuth, string program) : this(authurl, useHttpAuth ? authusername(Util.ProgramPath(program) + AuthFile) : "nofile.txt", useHttpAuth ? authpassword(Util.ProgramPath(program) + AuthFile) : "nofile.txt", 0) { }
        /// <summary>
        /// authenticate using url and login via http to said url, using specified http authentication file
        /// </summary>
        /// <param name="authurl"></param>
        /// <param name="authfilepath"></param>
        public Auth(string authurl, string authfilepath) : this(authurl, authusername(authfilepath), authpassword(authfilepath), 0) { }
        /// <summary>
        /// authenticate using url and login via http to said url, using specified http authentication file and specified expiration.
        /// </summary>
        /// <param name="authurl"></param>
        /// <param name="authfilepath"></param>
        /// <param name="expireAfterSeconds"></param>
        public Auth(string authurl, string authfilepath, int expireAfterSeconds) : this(authurl, authusername(authfilepath), authpassword(authfilepath), expireAfterSeconds) { }
        /// <summary>
        /// authenticate using given url and expire authenticates 
        /// </summary>
        /// <param name="authUrl"></param>
        /// <param name="expireAfterSeconds"></param>
        public Auth(string authUrl, int expireAfterSeconds) : this(authUrl, string.Empty, string.Empty, expireAfterSeconds) { }
        /// <summary>
        /// authenticate using given url, username and password with no expiration
        /// </summary>
        /// <param name="authUrl"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public Auth(string authUrl, string username, string password) : this(authUrl, username, password, 0) { }
        /// <summary>
        /// authenticate using given url, username/password and expiration settings
        /// </summary>
        /// <param name="authUrl"></param>
        /// <param name="username"></param>
        /// <param name="password"></param>
        /// <param name="expireAfterSeconds"></param>
        public Auth(string authUrl, string username, string password, int expireAfterSeconds) 
        { 
            url = authUrl;
            _un = username;
            _pw = password;
            _expiresec = expireAfterSeconds;
        }
        private bool hasuser { get { return _un != string.Empty; } }
        string _un = string.Empty;
        string _pw = string.Empty;
        string url = string.Empty;
        bool _lastauth = false;
        int _authtime = 0;
        int _expiresec = 0;
        /// <summary>
        /// number of seconds this authorization is good for before must be reauthorized
        /// (0 if not used)
        /// </summary>
        public int ExpireSeconds { get { return _expiresec; } }
        /// <summary>
        /// returns true if authorization has passed and not expired
        /// </summary>
        public bool isValid { get { return (_lastauth && (_expiresec==0)) || (_lastauth && (Util.FTDIFF(_authtime, Util.ToTLTime()) < _expiresec)); } }
        /// <summary>
        /// see if we're authorized on this machine
        /// </summary>
        /// <returns></returns>
        public bool isAuthorized() { return isAuthorized(Auth.GetCPUId()); }
        /// <summary>
        /// see if a given key is authorized
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool isAuthorized(string key)
        {
            return isAuthorized(key, true, false, true, false);
        }
        /// <summary>
        /// see if a given key is authorized, display dialog box and determine whether dialog box should pause program execution (eg if running on background thread which might terminate otherwise)
        /// </summary>
        /// <param name="key"></param>
        /// <param name="pause"></param>
        /// <returns></returns>
        public bool isAuthorized(string key,bool pause)
        {
            return isAuthorized(key, true, false, true,pause);
        }
        private void debug(string msg)
        {
            if (SendDebug != null)
                SendDebug(msg);
        }
        /// <summary>
        /// get debugging message from authentication
        /// </summary>
        public event TradeLink.API.DebugDelegate SendDebug;
        /// <summary>
        /// see if given key is authorized
        /// </summary>
        /// <param name="key">key</param>
        /// <param name="appendrandom">whether random parameter is appended to url to prevent results caching</param>
        /// <param name="throwexception">throw exceptions on errors</param>
        /// <param name="showerrorbox">show registration dialog if not authorized</param>
        /// <param name="pause">pause execution when showing dialog</param>
        /// <returns></returns>
        public bool isAuthorized(string key, bool appendrandom, bool throwexception, bool showerrorbox, bool pause)
        {
            bool reg = false;
            _lastauth = false;
            try
            {
                if (!hasuser)
                {
                    WebClient wc = new WebClient();
                    string rurl = url;
                    if (appendrandom)
                    {
                        if (!rurl.Contains("?"))
                            rurl += "?";
                        Random rand = new Random((int)DateTime.Now.Ticks);
                        string last = rurl.Substring(url.Length - 1, 1);
                        if ((last != "?") || (last != "&"))
                            rurl += "&";
                        rurl += "r=" + rand.Next().ToString();
                    }
                    string res = "";
                    res = wc.DownloadString(rurl);
                    reg = res.Contains(key);
                }
                else
                {
                    HttpWebRequest req = WebRequest.Create(url) as HttpWebRequest;
                    req.Credentials = new System.Net.NetworkCredential(_un, _pw);
                    req.PreAuthenticate = true;
                    SetBasicAuthHeader(req, _un, _pw);
                    if (req == null)
                    {
                        throw new NullReferenceException("request is not a http request");
                    }


                    // Process response
                    HttpWebResponse res = req.GetResponse() as HttpWebResponse;
                    System.IO.StreamReader responseReader = new System.IO.StreamReader(res.GetResponseStream());
                    string fullResponse = responseReader.ReadToEnd();
                    res.Close();
                    reg = fullResponse.Contains(key);

                }
            }
            catch (Exception ex)
            {
                debug("authexcept: "+ex.Message+" "+ex.StackTrace);
                reg = false;
            }
            _lastauth = reg;
            _authtime = Util.ToTLTime();
            string err = "Registration Key " + key + " not found. Contact your software provider to register software.";
            if (throwexception && !reg)
                throw new UnregisteredException(err);
            if (showerrorbox && !reg)
                PopupWindow.Show("Registration Error", err, true, pause);
            if (reg)
                debug("authorization succeeded: "+key);
            else
                debug(err);

            return reg;
        }
        
        private static string authpassword(string filepath)
        {
            try
            {
                System.IO.StreamReader sr = new System.IO.StreamReader(filepath);
                // skip user
                sr.ReadLine();
                // get password
                string r = sr.ReadLine();
                sr.Close();
                return r;
            }
            catch { }
            return string.Empty;
            
        }

        private static string authusername(string filepath)
        {
            try
            {
                System.IO.StreamReader sr = new System.IO.StreamReader(filepath);
                // get user
                string r = sr.ReadLine();
                sr.Close();
                return r;
            }
            catch { }
            return string.Empty;
            
        }

        private static void SetBasicAuthHeader(WebRequest req, String userName, String userPassword)
        {
            string authInfo = userName + ":" + userPassword;
            authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
            req.Headers["Authorization"] = "Basic " + authInfo;
        }

        /// <summary>
        /// Return processorId from first CPU in machine
        /// </summary>
        /// <returns>[string] ProcessorId</returns>
        public static string GetCPUId()
        {
            string cpuInfo = String.Empty;
            string temp = String.Empty;
            ManagementClass mc = new ManagementClass("Win32_Processor");
            ManagementObjectCollection moc = mc.GetInstances();
            foreach (ManagementObject mo in moc)
            {
                if (cpuInfo == String.Empty)
                {// only return cpuInfo from first CPU
                    cpuInfo = mo.Properties["ProcessorId"].Value.ToString();
                }
            }
            return cpuInfo;
        }
        /// <summary>
        /// hold authentication information.
        /// </summary>
        public struct AuthInfo
        {
            public string Username;
            public string Password;
            public bool isValid { get { return (Username != null) && (Password != null); } }
        }
        /// <summary>
        /// get authentication information from a file with username in first line and password in the second.
        /// </summary>
        /// <param name="filepath"></param>
        /// <returns></returns>
        public static AuthInfo GetAuthInfo(string filepath)
        {
            AuthInfo ai = new AuthInfo();
            try
            {
                System.IO.StreamReader sr = new System.IO.StreamReader(filepath);
                // skip user
                ai.Username = sr.ReadLine();
                // get password
                ai.Password = sr.ReadLine();
                sr.Close();
                return ai;
            }
            catch { }
            return ai;
        }
        /// <summary>
        /// get authentication information in the program path of PROGRAM
        /// </summary>
        /// <param name="PROGRAM"></param>
        /// <returns></returns>
        public static AuthInfo GetProgramAuth(string PROGRAM)
        {
            string filepath = Util.ProgramPath(PROGRAM) + TradeLink.AppKit.Auth.AuthFile;
            return GetAuthInfo(filepath);
        }
    }


    public class UnregisteredException : Exception
    {
        public UnregisteredException(string message) : base(message) { }
    }



}

