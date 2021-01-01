using dc = ejtictactoedemo.Lib.DataClasses;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Configuration;
using System.Threading.Tasks;
using System.Threading;
using EffortlessApi.SassyMQ.Lib;
using EffortlessAPIHostedCore;
using ejtictactoedemo.Lib.SqlDataManagement;

namespace EffortlessAPI.CRUD
{
    public partial class SMQSqlServerCRUDHandler
    {
        public List<String> Roles { get; }
        public SMQCRUDCoordinator CRUDCoordinator { get; }
        public string IPAddress { get; set; }
        public DateTime OnlineSince { get; private set; }

        public void Start()
        {
            this.CRUDCoordinator.WaitForComplete();
        }

        private DirectoryInfo BasePath { get; }
        public static string DBConnectionString { get; set; }

        private SqlDataManager APIWrapper;

        public SMQSqlServerCRUDHandler(string amqps, string dbcs, String rootPath)
        {
            this.OnlineSince = DateTime.UtcNow;
            this.BasePath = new DirectoryInfo(rootPath);
            SMQSqlServerCRUDHandler.DBConnectionString = dbcs;
            this.APIWrapper = new SqlDataManager(dbcs);
            this.Roles = "Guest,ServiceCoordinator,User,Admin,CRUDCoordinator".Split(",".ToCharArray()).ToList();

            this.CRUDCoordinator = new SMQCRUDCoordinator(amqps);
            this.CRUDCoordinator.GuestRequestTokenReceived += Cc_GuestCRUDRequestTokenReceived;
            this.CRUDCoordinator.GuestValidateTemporaryAccessTokenReceived += Cc_GuestCRUDValidateTemporaryAccessTokenReceived;
            this.CRUDCoordinator.GuestWhoAmIReceived += CRUDCoordinator_GuestWhoAmIReceived;
            this.CRUDCoordinator.GuestWhoAreYouReceived += CRUDCoordinator_GuestWhoAreYouReceived;
            this.CRUDCoordinator.GuestStoreTempFileReceived += CRUDCoordinator_GuestStoreTempFileReceived;
            //this.CRUDCoordinator.DefaultJsonSerializerSettings.ContractResolver = new AirtableExtensions.CustomContractResolver<StandardPayload>();
            this.InitActions();
        }


        private static String[] AcceptableExtensions = new String[] { ".jpg", ".jpeg", ".gif", ".png", ".zip", ".xslx", ".xls", ".csv", ".htm", ".html", ".xml", ".json", ".doc", ".docx", ".pdf", ".txt", ".mp3", ".mp4" };
        private string amqps;

        private void CRUDCoordinator_GuestStoreTempFileReceived(object sender, PayloadEventArgs e)
        {
            var fileId = String.Format("temp/{0}/{1}", Guid.NewGuid(), e.Payload.TempFileName);
            var tempFI = new FileInfo(Path.Combine(this.BasePath.FullName, "docs", fileId));
            if (!AcceptableExtensions.Contains(tempFI.Extension.ToLower()))
            {
                throw new Exception(String.Format("Extension {0} is not allowed to be stored temporarily", tempFI.Extension));
            }

            if (!tempFI.Directory.Exists) tempFI.Directory.Create();

            if (!ReferenceEquals(e.Payload.TempFileBinaryContents, null))
            {
                File.WriteAllBytes(tempFI.FullName, e.Payload.TempFileBinaryContents);
            }
            else if (!String.IsNullOrEmpty(e.Payload.TempFileTextContents))
            {
                File.WriteAllText(tempFI.FullName, e.Payload.TempFileTextContents);
            }
            else throw new Exception("Either `payload.TempFileBinaryContents` or `payload.TempFileTextContents` must be populated.");
            e.Payload.TempFileUrl = String.Format("http://{0}/{1}", this.IPAddress, fileId);

            Task.Factory.StartNew(() =>
            {
                Thread.Sleep(60000);
                if (tempFI.Exists)
                {
                    try
                    {
                        tempFI.Delete();
                    }
                    catch { } // Ignore errors
                }
            });
        }

        private void CRUDCoordinator_GuestWhoAreYouReceived(object sender, PayloadEventArgs e)
        {
            if (!String.IsNullOrEmpty(e.Payload.WhoAreYouRelativeUrl))
            {
                var fileInfo = new FileInfo(Path.Combine(this.BasePath.FullName, e.Payload.WhoAreYouRelativeUrl));
                if (fileInfo.Exists &&
                    fileInfo.FullName.StartsWith(this.BasePath.FullName) &&
                    (fileInfo.FullName.IndexOf("DotNet", StringComparison.OrdinalIgnoreCase) == -1))
                {
                    if (e.Payload.WhoAreYouIsBinary.HasValue && e.Payload.WhoAreYouIsBinary.Value)
                    {
                        e.Payload.WhoAreYouBinaryFileContents = File.ReadAllBytes(fileInfo.FullName);
                    }
                    else
                    {
                        e.Payload.WhoAreYouTextFileContents = File.ReadAllText(fileInfo.FullName);
                    }
                }
                else e.Payload.ErrorMessage = String.Format("Invalid path: {0}", fileInfo.FullName) +
                        (fileInfo.Exists ? "" : " (file not found)");
            }
            e.Payload.SchemaDocsUrl = String.Format("http://{0}/Schema/SinglePageDocs.html", this.IPAddress);
            e.Payload.SassyMQDocsUrl = String.Format("http://{0}/SassyMQ/ReadMe.html", this.IPAddress);
            e.Payload.SassyMQLexiconUrl = String.Format("http://{0}/SassyMQ/Lexicon.smql.json", this.IPAddress);
            e.Payload.SassyMQJsActorsUrl = String.Format("http://{0}/SassyMQ/jsActors/", this.IPAddress);
            e.Payload.SassyMQJsActorsByName = new Dictionary<String, string>();
            foreach (var role in this.Roles)
            {
                e.Payload.SassyMQJsActorsByName[role] = string.Format("http://{0}/SassyMQ/jsActors/smq{1}.js", this.IPAddress, role);
            }
            e.Payload.OnlineSince = this.OnlineSince;
            e.Payload.AppVersion = ConfigurationManager.AppSettings["version"];
        }

        private void CRUDCoordinator_GuestWhoAmIReceived(object sender, PayloadEventArgs e)
        {
            e.Payload.SingletonAppUser = e.Payload.AccessToken.GetJWT<dc.User>();
            e.Payload.OnlineSince = this.OnlineSince;
        }

        private dc.User CheckPayload(StandardPayload payload, string role, string eventName)
        {
            var apiUser = payload.AccessToken.GetJWT<dc.User>();
            if (!String.IsNullOrEmpty(payload.AirtableWhere))
            {
                payload.AirtableWhere = payload.AirtableWhere.Replace("$User.EmailAddress$", apiUser.EmailAddress);
            }
            
            if ((!apiUser.Roles.Contains(role)) && !apiUser.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase)) 
            {
                throw new UnauthorizedAccessException(String.Format("{0} requires {1} access", eventName, role));
            }
            else return apiUser;
        }

        private static bool CheckPayloadForPasswordMismatches(dc.User apiUser, string payloadSHA256DemoPassword, string payloadDemoPassword)
        {// AppUser  based on AppUser
                return false;
                                
        }

        private void Cc_GuestCRUDValidateTemporaryAccessTokenReceived(object sender, PayloadEventArgs e)
        {
            var apiUsers = this.APIWrapper.GetAllUsers<dc.User>("EmailAddress = '" + e.Payload.EmailAddress + "'");
            var matchingUser = apiUsers.FirstOrDefault(fodUser => (fodUser != null) && (fodUser.EmailAddress == e.Payload.EmailAddress));
            
            bool passwordMismatch = CheckPayloadForPasswordMismatches(matchingUser, e.Payload.SHA256DemoPassword, e.Payload.DemoPassword);
            
            if (!ReferenceEquals(matchingUser, null) && !passwordMismatch)
            {
                e.Payload.AccessToken = matchingUser.GetJwt();
            }
        }

        private void Cc_GuestCRUDRequestTokenReceived(object sender, PayloadEventArgs e)
        {
            throw new NotImplementedException();
            //var apiUsers = this.APIWrapper.GetAppUsers();
            //var matchingUser = apiUsers.FirstOrDefault(fodUser => fodUser.EmailAddress == e.Payload.EmailAddress);
            //if (!ReferenceEquals(matchingUser, null))
            //{
            //    e.Payload.AccessToken = matchingUser.GetJwt();
            //}
            //e.Payload.OnlineSince = this.OnlineSince;            
        }
    }
}