using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Web.Http;
using TicketSystemApi.Services;

namespace TicketSystemApi.Controllers
{
    [Authorize]
    [RoutePrefix("customers")]
    public class CaseCompanyReportController : ApiController
    {
        private const string CLAIM_USERNAME = "crm_username";
        private const string CLAIM_PASSWORD = "crm_password";

        // =====================================================
        // ACCOUNT ENDPOINT
        // GET /customers/accounts?page=1&pageSize=50
        // =====================================================
        [HttpGet]
        [Route("accounts")]
        public IHttpActionResult GetAccountTickets(int page = 1, int pageSize = 50)
        {
            return GetCompanyTickets("account", page, pageSize);
        }

        // =====================================================
        // CONTACT ENDPOINT
        // GET /customers/contacts?page=1&pageSize=50
        // =====================================================
        [HttpGet]
        [Route("contacts")]
        public IHttpActionResult GetContactTickets(int page = 1, int pageSize = 50)
        {
            return GetCompanyTickets("contact", page, pageSize);
        }

        // =====================================================
        // CORE LOGIC
        // =====================================================
        private IHttpActionResult GetCompanyTickets(string customerType, int page, int pageSize)
        {
            try
            {
                // ===== AUTH =====
                var identity = (ClaimsIdentity)User.Identity;
                var username = identity.FindFirst(CLAIM_USERNAME)?.Value;
                var password = identity.FindFirst(CLAIM_PASSWORD)?.Value;

                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return Unauthorized();

                var service = new CrmService().GetService1(username, password);

                // ===== FETCH INCIDENTS =====
                var query = new QueryExpression("incident")
                {
                    ColumnSet = new ColumnSet(true),
                    PageInfo = new PagingInfo
                    {
                        PageNumber = page,
                        Count = pageSize
                    },
                    Orders =
                    {
                        new OrderExpression("modifiedon", OrderType.Descending)
                    }
                };

                query.Criteria.AddCondition("customerid", ConditionOperator.NotNull);

                var result = service.RetrieveMultiple(query);

                // ===== PROJECT TICKETS =====
                var tickets = result.Entities
                    .Where(e =>
                        e.GetAttributeValue<EntityReference>("customerid")?.LogicalName == customerType)
                    .Select(e => new
                    {
                        TicketID = e.GetAttributeValue<string>("ticketnumber"),
                        CreatedBy = e.GetAttributeValue<EntityReference>("createdby")?.Name,
                        AgentName = e.GetAttributeValue<EntityReference>("ownerid")?.Name,

                        CustomerID = e.GetAttributeValue<EntityReference>("customerid").Id,
                        CustomerName = e.GetAttributeValue<EntityReference>("customerid").Name,
                        CustomerCrNumber = customerType == "account"
                            ? GetCustomerCrNumber(service, e.GetAttributeValue<EntityReference>("customerid"))
                            : null,

                        CreatedOn = ConvertToKsaTime(e.GetAttributeValue<DateTime?>("createdon")),
                        TicketType = e.GetAttributeValue<EntityReference>("new_tickettype")?.Name,
                        MineralClass = e.FormattedValues.Contains("new_class") ? e.FormattedValues["new_class"] : null,
                        Category = e.GetAttributeValue<EntityReference>("new_tickettype")?.Name,
                        SubCategory1 = e.GetAttributeValue<EntityReference>("new_mainclassification")?.Name,
                        SubCategory2 = e.GetAttributeValue<EntityReference>("new_subclassificationitem")?.Name,
                        Status = e.FormattedValues.Contains("statuscode") ? e.FormattedValues["statuscode"] : null,
                        TicketModifiedDateTime = ConvertToKsaTime(e.GetAttributeValue<DateTime?>("modifiedon")),
                        Department = e.GetAttributeValue<EntityReference>("new_businessunitid")?.Name,
                        TicketChannel = e.FormattedValues.Contains("new_ticketsubmissionchannel")
                            ? e.FormattedValues["new_ticketsubmissionchannel"]
                            : null,
                        Description = e.GetAttributeValue<string>("description"),
                        ModifiedBy = e.GetAttributeValue<EntityReference>("modifiedby")?.Name,
                        Priority = e.FormattedValues.Contains("prioritycode") ? e.FormattedValues["prioritycode"] : null,
                        CurrentStage = MapStatusCodeToStage(e),
                        EscalationLevel = GetEscalationLevel(e),
                        ReopenedOn = ConvertToKsaTime(e.GetAttributeValue<DateTime?>("new_reopendatetime"))?.ToString("yyyy-MM-dd HH:mm:ss"),

                        // ✅ SLA Success Times
                        AssignmentSucceededOn = ConvertToKsaTime(e.GetAttributeValue<DateTime?>("new_assignmentsucceededon"))?.ToString("yyyy-MM-dd HH:mm:ss"),
                        ProcessingSucceededOn = ConvertToKsaTime(e.GetAttributeValue<DateTime?>("new_processingsucceededon"))?.ToString("yyyy-MM-dd HH:mm:ss"),
                        SolutionVerificationSucceededOn = ConvertToKsaTime(e.GetAttributeValue<DateTime?>("new_solutionverificationsucceededon"))?.ToString("yyyy-MM-dd HH:mm:ss"),


                        // ✅ SLA Violations - Approval & Forwarding
                        AssignmentSlaViolationL1 = e.GetAttributeValue<bool?>("new_assignmentslaviolationl1") == true ? "Yes" : "No",
                        AssignmentSlaViolationL2 = e.GetAttributeValue<bool?>("new_assignmentslaviolationl2") == true ? "Yes" : "No",
                        AssignmentSlaViolationL3 = e.GetAttributeValue<bool?>("new_assignmentslaviolationl3") == true ? "Yes" : "No",

                        // ✅ SLA Violations - Processing
                        ProcessingSlaViolationL1 = e.GetAttributeValue<bool?>("new_slaviolationl1") == true ? "Yes" : "No",
                        ProcessingSlaViolationL2 = e.GetAttributeValue<bool?>("new_slaviolationl2") == true ? "Yes" : "No",
                        ProcessingSlaViolationL3 = e.GetAttributeValue<bool?>("new_slaviolationl3") == true ? "Yes" : "No",
                        ProcessingSlaViolationL4 = e.GetAttributeValue<bool?>("new_slaviolationl4") == true ? "Yes" : "No",

                        // ✅ SLA Violations - Verification
                        VerificationSlaViolationL1 = e.GetAttributeValue<bool?>("new_verificationslaviolationl1") == true ? "Yes" : "No",
                        VerificationSlaViolationL2 = e.GetAttributeValue<bool?>("new_verificationslaviolationl2") == true ? "Yes" : "No",
                        VerificationSlaViolationL3 = e.GetAttributeValue<bool?>("new_verificationslaviolationl3") == true ? "Yes" : "No"
                    })
                    .ToList();

                // ================= STEP 2: FETCH ACCOUNT / CONTACT =================
                var customerIds = tickets.Select(t => t.CustomerID).Distinct().ToList();

                Dictionary<Guid, Entity> accountLookup = new Dictionary<Guid, Entity>();
                Dictionary<Guid, Entity> contactLookup = new Dictionary<Guid, Entity>();

                if (customerType == "account" && customerIds.Any())
                {
                    var accQuery = new QueryExpression("account")
                    {
                        ColumnSet = new ColumnSet(
                            "accountid",
                            "name",
                            "emailaddress1",
                            "new_crnumber",
                            "new_companyrepresentativephonenumber"
                        ),
                        Criteria =
                        {
                            Conditions =
                            {
                                new ConditionExpression(
                                    "accountid",
                                    ConditionOperator.In,
                                    customerIds.ToArray()
                                )
                            }
                        }
                    };

                    accountLookup = service
                        .RetrieveMultiple(accQuery)
                        .Entities
                        .ToDictionary(a => a.Id, a => a);
                }

                if (customerType == "contact" && customerIds.Any())
                {
                    var conQuery = new QueryExpression("contact")
                    {
                        ColumnSet = new ColumnSet(
                            "contactid",
                            "fullname",
                            "emailaddress1",
                            "mobilephone"
                        ),
                        Criteria =
                        {
                            Conditions =
                            {
                                new ConditionExpression(
                                    "contactid",
                                    ConditionOperator.In,
                                    customerIds.ToArray()
                                )
                            }
                        }
                    };

                    contactLookup = service
                        .RetrieveMultiple(conQuery)
                        .Entities
                        .ToDictionary(c => c.Id, c => c);
                }

                // ================= GROUP BY COMPANY =================
                var grouped = tickets
                    .GroupBy(t => new { t.CustomerID, t.CustomerName, t.CustomerCrNumber })
                    .Select(g =>
                    {
                        string email = null;
                        string phone = null;
                        string cr = g.Key.CustomerCrNumber;

                        if (customerType == "account" && accountLookup.ContainsKey(g.Key.CustomerID))
                        {
                            var a = accountLookup[g.Key.CustomerID];
                            email = a.GetAttributeValue<string>("emailaddress1");
                            phone = a.GetAttributeValue<string>("new_companyrepresentativephonenumber");
                            cr = a.GetAttributeValue<string>("new_crnumber");
                        }
                        else if (customerType == "contact" && contactLookup.ContainsKey(g.Key.CustomerID))
                        {
                            var c = contactLookup[g.Key.CustomerID];
                            email = c.GetAttributeValue<string>("emailaddress1");
                            phone = c.GetAttributeValue<string>("mobilephone");
                        }

                        return new
                        {
                            Customers = new
                            {
                                id = g.Key.CustomerID,
                                type = customerType == "account" ? "Account" : "Contact",
                                name = g.Key.CustomerName,
                                crNumber = cr,
                                email = email,
                                phone = phone
                            },
                            tickets = g.ToList()
                        };
                    })
                    .ToList();

                return Ok(new
                {
                    page,
                    pageSize,
                    records = grouped
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Company report failed: {ex.Message}"));
            }
        }

        // =====================================================
        // HELPERS
        // =====================================================
        private DateTime? ConvertToKsaTime(DateTime? utcDate)
        {
            if (!utcDate.HasValue)
                return null;

            TimeZoneInfo ksaZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDate.Value, DateTimeKind.Utc), ksaZone);
        }
        

        private string GetCustomerCrNumber(IOrganizationService service, EntityReference customerRef)
        {
            try
            {
                var acc = service.Retrieve("account", customerRef.Id, new ColumnSet("new_crnumber"));
                return acc.GetAttributeValue<string>("new_crnumber");
            }
            catch { return null; }
        }

        private string MapStatusCodeToStage(Entity e)
        {
            var status = e.GetAttributeValue<OptionSetValue>("statuscode")?.Value;
            switch (status)
            {
                case 100000000: return "Ticket Creation";
                case 100000006: return "Approval and Forwarding";
                case 100000002: return "Solution Verification";
                case 100000008: return "Processing";
                case 1: return "Processing- Department";
                case 100000001: return "Return to Customer";
                case 100000003: return "Ticket Closure";
                case 100000005: return "Ticket Reopen";
                case 5: return "Problem Solved";
                case 1000: return "Information Provided";
                case 6: return "Cancelled";
                case 2000: return "Merged";
                case 100000007: return "Close";
                default: return "Unknown";
            }
        }

        private string GetEscalationLevel(Entity e)
        {

            // Track escalation level text
            string escalation = "No Escalation";

            // ✅ Approval (check all levels, update if found)
            if (e.GetAttributeValue<bool?>("new_assignmentslaviolationl1") == true) escalation = "Approval Escalation - Level 1";
            if (e.GetAttributeValue<bool?>("new_assignmentslaviolationl2") == true) escalation = "Approval Escalation - Level 2";
            if (e.GetAttributeValue<bool?>("new_assignmentslaviolationl3") == true) escalation = "Approval Escalation - Level 3";

            // ✅ Processing (overwrite if later escalation found)
            if (e.GetAttributeValue<bool?>("new_slaviolationl1") == true) escalation = "Processing Escalation - Level 1";
            if (e.GetAttributeValue<bool?>("new_slaviolationl2") == true) escalation = "Processing Escalation - Level 2";
            if (e.GetAttributeValue<bool?>("new_slaviolationl3") == true) escalation = "Processing Escalation - Level 3";
            if (e.GetAttributeValue<bool?>("new_slaviolationl4") == true) escalation = "Processing Escalation - Level 4";

            // ✅ Verification (overwrite if even later escalation found)
            if (e.GetAttributeValue<bool?>("new_verificationslaviolationl1") == true) escalation = "Verification Escalation - Level 1";
            if (e.GetAttributeValue<bool?>("new_verificationslaviolationl2") == true) escalation = "Verification Escalation - Level 2";
            if (e.GetAttributeValue<bool?>("new_verificationslaviolationl3") == true) escalation = "Verification Escalation - Level 3";

            return escalation;
        }
    }
}
