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
    [RoutePrefix("customers")]
    public class CustomerTicketController : ApiController
    {
        private const string CLAIM_USERNAME = "crm_username";
        private const string CLAIM_PASSWORD = "crm_password";

        /// <summary>
        /// GET /customers/CustomerTickets?page=1&pageSize=50
        /// </summary>
        [HttpGet]
        [Route("CustomerTickets")]
        public IHttpActionResult GetAllCustomersWithTickets(
            int page = 1,
            int pageSize = 50)
        {
            try
            {
                // ===================== AUTH =====================
                var identity = (ClaimsIdentity)User.Identity;
                var username = identity.FindFirst(CLAIM_USERNAME)?.Value;
                var password = identity.FindFirst(CLAIM_PASSWORD)?.Value;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return Unauthorized();

                var service = new CrmService().GetService1(username, password);

                // ===================== STEP 1: GET TOTAL COUNTS =====================
                int totalAccounts = GetTotalCount(service, "account");
                int totalContacts = GetTotalCount(service, "contact");
                int totalCustomers = totalAccounts + totalContacts;

                // ===================== STEP 2: CALCULATE GLOBAL PAGING =====================
                int skip = (page - 1) * pageSize;
                int take = pageSize;

                var customers = new List<CustomerWithTicketsDto>();

                // ===================== STEP 3: FETCH ACCOUNTS =====================
                if (skip < totalAccounts)
                {
                    int accountPage = (skip / pageSize) + 1;

                    var accountQuery = new QueryExpression("account")
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
                            PageNumber = accountPage,
                            Count = pageSize
                        }
                    };

                    var accountResult = service.RetrieveMultiple(accountQuery);

                    customers.AddRange(accountResult.Entities.Select(a => new CustomerWithTicketsDto
                    {
                        CustomerId = a.Id,
                        CustomerType = "Account",
                        Name = a.GetAttributeValue<string>("name"),
                        Email = a.GetAttributeValue<string>("emailaddress1"),
                        MobileOrPhone = a.GetAttributeValue<string>("new_companyrepresentativephonenumber"),
                        CrNumber = a.GetAttributeValue<string>("new_crnumber"),
                        Tickets = new List<TicketSummaryDto>()
                    }));

                    take -= customers.Count;
                }

                // ===================== STEP 4: FETCH CONTACTS =====================
                if (take > 0)
                {
                    int contactSkip = Math.Max(0, skip - totalAccounts);
                    int contactPage = (contactSkip / pageSize) + 1;

                    var contactQuery = new QueryExpression("contact")
                    {
                        ColumnSet = new ColumnSet(
                            "contactid",
                            "fullname",
                            "emailaddress1",
                            "mobilephone"
                        ),
                        PageInfo = new PagingInfo
                        {
                            PageNumber = contactPage,
                            Count = take
                        }
                    };

                    var contactResult = service.RetrieveMultiple(contactQuery);

                    customers.AddRange(contactResult.Entities.Select(c => new CustomerWithTicketsDto
                    {
                        CustomerId = c.Id,
                        CustomerType = "Contact",
                        Name = c.GetAttributeValue<string>("fullname"),
                        Email = c.GetAttributeValue<string>("emailaddress1"),
                        MobileOrPhone = c.GetAttributeValue<string>("mobilephone"),
                        CrNumber = null,
                        Tickets = new List<TicketSummaryDto>()
                    }));
                }

                if (!customers.Any())
                {
                    return Ok(new
                    {
                        Page = page,
                        PageSize = pageSize,
                        //TotalRecords = totalCustomers,
                        Records = customers
                    });
                }

                // ===================== STEP 5: FETCH ALL TICKETS (ONE CALL) =====================
                var customerIds = customers.Select(c => c.CustomerId).ToArray();

                var ticketQuery = new QueryExpression("incident")
                {
                    ColumnSet = new ColumnSet("ticketnumber", "title", "customerid"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "customerid",
                                ConditionOperator.In,
                                customerIds
                            )
                        }
                    }
                };

                var tickets = service.RetrieveMultiple(ticketQuery).Entities;

                // ===================== STEP 6: MAP TICKETS =====================
                var ticketLookup = tickets
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

                foreach (var customer in customers)
                {
                    if (ticketLookup.ContainsKey(customer.CustomerId))
                        customer.Tickets = ticketLookup[customer.CustomerId];
                }

                // ===================== RESPONSE =====================
                return Ok(new
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalRecords = totalCustomers,
                    Records = customers
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception($"CustomerTickets API failed: {ex.Message}")
                );
            }
        }

        // ===================== TOTAL COUNT HELPER =====================
        private int GetTotalCount(IOrganizationService service, string entityName)
        {
            var qe = new QueryExpression(entityName)
            {
                ColumnSet = new ColumnSet(false),
                PageInfo = new PagingInfo { PageNumber = 1, Count = 1 }
            };

            return service.RetrieveMultiple(qe).TotalRecordCount;
        }
    }
}
