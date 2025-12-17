using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Web.Http;
using TicketSystemApi.Models;
using TicketSystemApi.Services;

namespace TicketSystemApi.Controllers
{
    [Authorize]
    [RoutePrefix("Customers")]
    public class CustomerCompanyController : ApiController
    {
        private const string CLAIM_USERNAME = "crm_username";
        private const string CLAIM_PASSWORD = "crm_password";

        // =====================================================
        // ROUTE 1: ACCOUNTS AS COMPANY
        // GET /customers/companies/accounts?page=1&pageSize=50
        // =====================================================
        [HttpGet]
        [Route("accounts")]
        public IHttpActionResult GetCompaniesFromAccounts(int page = 1, int pageSize = 50)
        {
            var service = GetCrmService();
            if (service == null) return Unauthorized();

            var query = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet(
                    "accountid",
                    "name",
                    "emailaddress1",
                    "new_crnumber",
                    "new_companyrepresentativephonenumber"
                ),
                PageInfo = new PagingInfo
                {
                    PageNumber = page,
                    Count = pageSize
                }
            };

            var result = service.RetrieveMultiple(query);

            var companies = result.Entities.Select(a => new CustomerWithTicketsDto
            {
                CompanyId = a.Id,
                CompanyType = "Account",
                CompanyName = a.GetAttributeValue<string>("name"),
                Email = a.GetAttributeValue<string>("emailaddress1"),
                Phone = a.GetAttributeValue<string>("new_companyrepresentativephonenumber"),
                CrNumber = a.GetAttributeValue<string>("new_crnumber")
            }).ToList();

            AttachTickets(service, companies);

            return Ok(new
            {
                Page = page,
                PageSize = pageSize,
                //HasMoreRecords = result.MoreRecords,
                Records = companies
            });
        }

        // =====================================================
        // ROUTE 2: CONTACTS AS COMPANY
        // GET /customers/companies/contacts?page=1&pageSize=50
        // =====================================================
        [HttpGet]
        [Route("contacts")]
        public IHttpActionResult GetCompaniesFromContacts(int page = 1, int pageSize = 50)
        {
            var service = GetCrmService();
            if (service == null) return Unauthorized();

            var query = new QueryExpression("contact")
            {
                ColumnSet = new ColumnSet(
                    "contactid",
                    "fullname",
                    "emailaddress1",
                    "mobilephone"
                ),
                PageInfo = new PagingInfo
                {
                    PageNumber = page,
                    Count = pageSize
                }
            };

            var result = service.RetrieveMultiple(query);

            var companies = result.Entities.Select(c => new CustomerWithTicketsDto
            {
                CompanyId = c.Id,
                CompanyType = "Contact",
                CompanyName = c.GetAttributeValue<string>("fullname"),
                Email = c.GetAttributeValue<string>("emailaddress1"),
                Phone = c.GetAttributeValue<string>("mobilephone"),
                CrNumber = null
            }).ToList();

            AttachTickets(service, companies);

            return Ok(new
            {
                Page = page,
                PageSize = pageSize,
                //HasMoreRecords = result.MoreRecords,
                Records = companies
            });
        }

        // =====================================================
        // SHARED HELPERS
        // =====================================================
        private IOrganizationService GetCrmService()
        {
            var identity = (ClaimsIdentity)User.Identity;
            var username = identity.FindFirst(CLAIM_USERNAME)?.Value;
            var password = identity.FindFirst(CLAIM_PASSWORD)?.Value;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            return new CrmService().GetService1(username, password);
        }

        private void AttachTickets(
            IOrganizationService service,
            List<CustomerWithTicketsDto> companies)
        {
            if (!companies.Any()) return;

            var ids = companies.Select(c => c.CompanyId).ToArray();

            var ticketQuery = new QueryExpression("incident")
            {
                ColumnSet = new ColumnSet("ticketnumber", "title", "customerid"),
                Criteria =
                {
                    Conditions =
                    {
                        new ConditionExpression(
                            "customerid",
                            ConditionOperator.In,
                            ids
                        )
                    }
                }
            };

            var tickets = service.RetrieveMultiple(ticketQuery).Entities;

            var lookup = tickets
                .Where(t => t.Contains("customerid"))
                .GroupBy(t => t.GetAttributeValue<EntityReference>("customerid").Id)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(t => new TicketSummaryDto
                    {
                        TicketNumber = t.GetAttributeValue<string>("ticketnumber"),
                        Title = t.GetAttributeValue<string>("title")
                    }).ToList()
                );

            foreach (var company in companies)
            {
                if (lookup.ContainsKey(company.CompanyId))
                    company.Tickets = lookup[company.CompanyId];
            }
        }
    }
}
