using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.ServiceModel.Description;

namespace TicketSystemApi.Services
{
    public static class CrmHelper
    {
        public static IOrganizationService GetCrmSystemService()
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                var orgServiceUrl = System.Configuration.ConfigurationManager.AppSettings["CrmServiceUrl"];
                var crmUser = System.Configuration.ConfigurationManager.AppSettings["CrmUsername"];
                var crmPass = System.Configuration.ConfigurationManager.AppSettings["CrmPassword"];

                // ✅ Correct connection string for IFD
                string connString = $@"
                    AuthType=IFD;
                    Url={orgServiceUrl};
                    Username={crmUser};
                    Password={crmPass};
                    Domain=crm-esnad.com;
                ";

                var crmServiceClient = new CrmServiceClient(connString);

                if (!crmServiceClient.IsReady)
                    throw new Exception($"CRM connection failed: {crmServiceClient.LastCrmError}");

                return crmServiceClient.OrganizationWebProxyClient != null
                    ? (IOrganizationService)crmServiceClient.OrganizationWebProxyClient
                    : (IOrganizationService)crmServiceClient.OrganizationServiceProxy;
            }
            catch (Exception ex)
            {
                throw new Exception($"CRM system user connection failed: {ex.Message}");
            }
        }
        public static IOrganizationService AuthenticateUser(string username, string password)
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;

                var orgServiceUrl = System.Configuration.ConfigurationManager.AppSettings["CrmServiceUrl"];
                var creds = new ClientCredentials();
                creds.UserName.UserName = username;
                creds.UserName.Password = password;

                var serviceProxy = new OrganizationServiceProxy(new Uri(orgServiceUrl), null, creds, null);
                serviceProxy.EnableProxyTypes();

                return serviceProxy;
            }
            catch (Exception ex)
            {
                throw new Exception($"CRM user authentication failed: {ex.Message}");
            }
        }
    }
}
