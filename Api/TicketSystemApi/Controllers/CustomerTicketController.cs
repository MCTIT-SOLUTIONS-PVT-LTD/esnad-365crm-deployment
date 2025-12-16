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
            int pageSize = 100)
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

                var result = new List<CustomerWithTicketsDto>();

                // ===================== ACCOUNTS =====================
                var accountQuery = new QueryExpression("account")
                {
                    ColumnSet = new ColumnSet(
                        "accountid",
                        "name",
                        "emailaddress1",
                        "new_crnumber",
                        "new_companyrepresentativephonenumber"
                    )
                };

                var accounts = service.RetrieveMultiple(accountQuery);

                foreach (var acc in accounts.Entities)
                {
                    var customer = new CustomerWithTicketsDto
                    {
                        CustomerId = acc.Id,
                        CustomerType = "Account",
                        Name = acc.GetAttributeValue<string>("name"),
                        Email = acc.GetAttributeValue<string>("emailaddress1"),
                        MobileOrPhone = acc.GetAttributeValue<string>("new_companyrepresentativephonenumber"),
                        CrNumber = acc.GetAttributeValue<string>("new_crnumber"),
                        Tickets = GetTickets(service, acc.Id)
                    };

                    result.Add(customer);
                }

                // ===================== CONTACTS =====================
                var contactQuery = new QueryExpression("contact")
                {
                    ColumnSet = new ColumnSet(
                        "contactid",
                        "fullname",
                        "emailaddress1",
                        "mobilephone"
                    )
                };

                var contacts = service.RetrieveMultiple(contactQuery);

                foreach (var con in contacts.Entities)
                {
                    var customer = new CustomerWithTicketsDto
                    {
                        CustomerId = con.Id,
                        CustomerType = "Contact",
                        Name = con.GetAttributeValue<string>("fullname"),
                        Email = con.GetAttributeValue<string>("emailaddress1"),
                        MobileOrPhone = con.GetAttributeValue<string>("mobilephone"),
                        CrNumber = null,
                        Tickets = GetTickets(service, con.Id)
                    };

                    result.Add(customer);
                }

                // ===================== PAGINATION =====================
                var totalRecords = result.Count;

                var pagedRecords = result
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return Ok(new
                {
                    Page = page,
                    PageSize = pageSize,
                    TotalRecords = totalRecords,
                    Records = pagedRecords
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception($"CustomerTickets API failed: {ex.Message}")
                );
            }
        }

        // ===================== HELPER =====================
        private List<TicketSummaryDto> GetTickets(
            IOrganizationService service,
            Guid customerId)
        {
            var tickets = new List<TicketSummaryDto>();

            var qe = new QueryExpression("incident")
            {
                ColumnSet = new ColumnSet("ticketnumber", "title"),
                Criteria = new FilterExpression()
            };

            qe.Criteria.AddCondition(
                "customerid",
                ConditionOperator.Equal,
                customerId
            );

            var incidents = service.RetrieveMultiple(qe);

            foreach (var i in incidents.Entities)
            {
                tickets.Add(new TicketSummaryDto
                {
                    TicketNumber = i.GetAttributeValue<string>("ticketnumber"),
                    Title = i.GetAttributeValue<string>("title")
                });
            }

            return tickets;
        }
    }
}
