using System;
using System.ServiceModel.Description;
using System.Configuration;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;

namespace TicketSystemApi.Services
{

    public interface ICrmService
    {
        IOrganizationService GetService();
    }

    public class CrmService : ICrmService
        {
        // ✅ Static CRM system user (used everywhere except report)
        public IOrganizationService GetService()
        {
            try
            {
                string crmServiceUrl = ConfigurationManager.AppSettings["CrmServiceUrl"];
                if (string.IsNullOrEmpty(crmServiceUrl))
                    throw new Exception("Missing app setting: CrmServiceUrl");

                Uri serviceUri = new Uri(crmServiceUrl); // Use CrmServiceUrl for static login

                var username = ConfigurationManager.AppSettings["CrmUsername"];
                var password = ConfigurationManager.AppSettings["CrmPassword"];

                ClientCredentials credentials = new ClientCredentials();
                credentials.UserName.UserName = username;
                credentials.UserName.Password = password;

                var proxy = new OrganizationServiceProxy(serviceUri, null, credentials, null);
                proxy.EnableProxyTypes();

                return (IOrganizationService)proxy;
            }
            catch (Exception ex)
            {
                throw new Exception($"CRM system user authentication failed: {ex.Message}");
            }
        }

        // Dynamic login for Report API using CrmBaseUrl (OAuth login)
        public IOrganizationService GetServiceAsUser(string username, string password)
        {
            try
            {
                string crmBaseUrl = ConfigurationManager.AppSettings["CrmBaseUrl"];
                if (string.IsNullOrEmpty(crmBaseUrl))
                    throw new Exception("Missing app setting: CrmBaseUrl");

                Uri serviceUri = new Uri(crmBaseUrl); // Use CrmBaseUrl for OAuth-based login

                ClientCredentials credentials = new ClientCredentials();
                credentials.UserName.UserName = username;
                credentials.UserName.Password = password;

                var proxy = new OrganizationServiceProxy(serviceUri, null, credentials, null);
                proxy.EnableProxyTypes();

                return (IOrganizationService)proxy;
            }
            catch (Exception ex)
            {
                throw new Exception($"CRM user authentication failed: {ex.Message}");
            }
        }
        public IOrganizationService GetService1(string username, string password)
        {
            try
            {
                string crmServiceUrl = ConfigurationManager.AppSettings["CrmServiceUrl"];
                if (string.IsNullOrEmpty(crmServiceUrl))
                    throw new Exception("Missing app setting: CrmServiceUrl");

                Uri serviceUri = new Uri(crmServiceUrl); // Use CrmServiceUrl for static login

                ClientCredentials credentials = new ClientCredentials();
                credentials.UserName.UserName = username;
                credentials.UserName.Password = password;

                var proxy = new OrganizationServiceProxy(serviceUri, null, credentials, null);
                proxy.EnableProxyTypes();

                return (IOrganizationService)proxy;
            }
            catch (Exception ex)
            {
                throw new Exception($"CRM user authentication failed: {ex.Message}");
            }
        }

    }
}
