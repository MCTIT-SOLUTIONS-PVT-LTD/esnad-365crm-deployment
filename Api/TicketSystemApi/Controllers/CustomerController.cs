﻿using System;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Configuration;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using TicketSystemApi.Services;
using TicketSystemApi.Models;

namespace TicketSystemApi.Controllers
{
    [RoutePrefix("customers")]
    public class CustomerController : ApiController
    {
        private readonly ICrmService _crmService;

        public CustomerController()
        {
            _crmService = new CrmService();
        }

        [HttpGet]
        [Route("by-ticket/{ticketNumber}")]
        public IHttpActionResult GetCustomerByTicket(string ticketNumber)
        {
            var authHeader = Request.Headers.Authorization;
            string expectedToken = ConfigurationManager.AppSettings["ApiBearerToken"];

            if (authHeader == null || authHeader.Scheme != "Bearer" || authHeader.Parameter != expectedToken)
                return Content(HttpStatusCode.Unauthorized,
                    ApiResponse<object>.Error("Unauthorized - Invalid bearer token"));

            if (string.IsNullOrWhiteSpace(ticketNumber))
                return Content(HttpStatusCode.BadRequest,
                    ApiResponse<object>.Error("Ticket number is required."));

            ticketNumber = ticketNumber.Trim().ToUpper();

            try
            {
                var service = _crmService.GetService();

                var query = new QueryExpression("incident")
                {
                    ColumnSet = new ColumnSet("ticketnumber", "customerid", "incidentid"),
                    Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("ticketnumber", ConditionOperator.Equal, ticketNumber)
                }
            }
                };

                var result = service.RetrieveMultiple(query);
                var incident = result.Entities.FirstOrDefault();

                if (incident == null)
                    return Content(HttpStatusCode.NotFound,
                        ApiResponse<object>.Error($"No case found for ticket number: {ticketNumber}"));

                if (!incident.Contains("customerid") || !(incident["customerid"] is EntityReference customerRef))
                    return Ok(ApiResponse<object>.Error("Customer is not linked with the specified case."));

                Entity customer = null;

                // Check whether the reference is Contact or Account
                if (customerRef.LogicalName == "contact")
                {
                    try
                    {
                        customer = service.Retrieve("contact", customerRef.Id,
                            new ColumnSet("firstname", "lastname", "emailaddress1"));
                    }
                    catch (Exception)
                    {
                        return Ok(ApiResponse<object>.Error("Customer record (Contact) does not exist."));
                    }
                }
                else if (customerRef.LogicalName == "account")
                {
                    try
                    {
                        customer = service.Retrieve("account", customerRef.Id,
                            new ColumnSet("name", "emailaddress1"));
                    }
                    catch (Exception)
                    {
                        return Ok(ApiResponse<object>.Error("Customer record (Account) does not exist."));
                    }
                }
                else
                {
                    return Ok(ApiResponse<object>.Error($"Unsupported customer type: {customerRef.LogicalName}"));
                }

                if (customer == null)
                    return Ok(ApiResponse<object>.Error("Customer record could not be retrieved."));

                if (customer == null)
                    return Ok(ApiResponse<object>.Error("Customer record could not be retrieved."));

                // 🔍 Check if feedback already exists for the case
                var feedbackQuery = new QueryExpression("new_customersatisfactionscore")
                {
                    ColumnSet = new ColumnSet("new_customersatisfactionscoreid"),
                    Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("new_csatcase", ConditionOperator.Equal, incident.Id)
                }
            }
                };

                var feedbackResult = service.RetrieveMultiple(feedbackQuery);
                if (feedbackResult.Entities.Any())
                {
                    return Content(HttpStatusCode.BadRequest,
                        ApiResponse<object>.Error("Feedback already submitted for this case."));
                }

                return Ok(ApiResponse<object>.Success(new
                {
                    CaseId = incident.Id,
                    TicketNumber = ticketNumber,
                    CustomerId = customer.Id,
                    FirstName = (customerRef.LogicalName == "contact") ? customer.GetAttributeValue<string>("firstname") : null,
                    LastName = (customerRef.LogicalName == "contact") ? customer.GetAttributeValue<string>("lastname") : null,
                    FullName = (customerRef.LogicalName == "account") ? customer.GetAttributeValue<string>("name") : null,
                    DisplayName = (customerRef.LogicalName == "contact")
                        ? $"{customer.GetAttributeValue<string>("firstname")} {customer.GetAttributeValue<string>("lastname")}".Trim()
                        : customer.GetAttributeValue<string>("name"),
                    Email = customer.GetAttributeValue<string>("emailaddress1")
                }, "Customer retrieved successfully"));

            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError,
                    ApiResponse<object>.Error($"CRM error: {ex.Message}"));
            }
        }

        [HttpPost]
        [Route("submit-feedback")]
        public IHttpActionResult SubmitCustomerFeedback([FromBody] CustomerFeedbackModel model)
        {
            var authHeader = Request.Headers.Authorization;
            string expectedToken = ConfigurationManager.AppSettings["ApiBearerToken"];

            if (authHeader == null || authHeader.Scheme != "Bearer" || authHeader.Parameter != expectedToken)
                return Content(HttpStatusCode.Unauthorized,
                    ApiResponse<object>.Error("Unauthorized - Invalid bearer token"));

            if (model == null || string.IsNullOrWhiteSpace(model.CaseId))
                return Content(HttpStatusCode.BadRequest,
                    ApiResponse<object>.Error("Case ID is required."));

            if (model.Rating < 1 || model.Rating > 5)
                return Content(HttpStatusCode.BadRequest,
                    ApiResponse<object>.Error("Rating must be between 1 and 5."));

            try
            {
                var service = _crmService.GetService();
                Guid caseGuid = new Guid(model.CaseId);

                // 🔍 Check if feedback already exists for this case
                var existingQuery = new QueryExpression("new_customersatisfactionscore")
                {
                    ColumnSet = new ColumnSet("new_customersatisfactionrating"),
                    Criteria =
            {
                Conditions =
                {
                    new ConditionExpression("new_csatcase", ConditionOperator.Equal, caseGuid)
                }
            }
                };

                var existingFeedback = service.RetrieveMultiple(existingQuery);
                if (existingFeedback.Entities.Any())
                {
                    return Content(HttpStatusCode.Conflict,
                        ApiResponse<object>.Error("Feedback already submitted for this case."));
                }

                // ✅ Create new feedback
                if (model.TimeAppropriate != 1 && model.TimeAppropriate != 2)
                    return Content(HttpStatusCode.BadRequest,
                        ApiResponse<object>.Error("Please answer whether the time taken was appropriate."));

                var commentValue = string.IsNullOrWhiteSpace(model.Comment) ? "No comments added by customer" : model.Comment.Trim();

                var feedback = new Entity("new_customersatisfactionscore");
                feedback["new_customersatisfactionrating"] = new OptionSetValue(model.Rating);
                feedback["new_comment"] = commentValue;
                feedback["new_customersatisfactionscore"] = commentValue;  // ⬅️ Additional field to store same comment
                feedback["new_csatcase"] = new EntityReference("incident", caseGuid);
                feedback["new_wasthetimetakentoprocesstheticketappropri"] = (model.TimeAppropriate == 1);

                var feedbackId = service.Create(feedback);

                return Ok(ApiResponse<object>.Success(new
                {
                    FeedbackId = feedbackId
                }, "Feedback submitted successfully"));
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError,
                    ApiResponse<object>.Error($"CRM error: {ex.Message}"));
            }
        }
        [HttpPost]
        [Route("visitor-feedback")]
        public IHttpActionResult SubmitVisitorFeedback([FromBody] VisitorFeedbackModel model)
        {
            var authHeader = Request.Headers.Authorization;
            string expectedToken = ConfigurationManager.AppSettings["ApiBearerToken"];

            if (authHeader == null || authHeader.Scheme != "Bearer" || authHeader.Parameter != expectedToken)
                return Content(HttpStatusCode.Unauthorized, ApiResponse<object>.Error("Unauthorized - Invalid bearer token"));

            if (model == null || (string.IsNullOrWhiteSpace(model.ContactId) && string.IsNullOrWhiteSpace(model.AccountId)))
                return Content(HttpStatusCode.BadRequest, ApiResponse<object>.Error("Either ContactId or AccountId is required."));

            try
            {
                var service = _crmService.GetService();
                var feedback = new Entity("new_satisfactionsurveysms");
                string linkedVia = "";

                // 🔹 Case 1: Contact feedback
                if (!string.IsNullOrWhiteSpace(model.ContactId))
                {
                    Guid contactId = new Guid(model.ContactId);

                    // Prevent duplicates
                    var existingQuery = new QueryExpression("new_satisfactionsurveysms")
                    {
                        ColumnSet = new ColumnSet("new_satisfactionsurveysmsid"),
                        Criteria =
                {
                    Conditions = { new ConditionExpression("new_satisfactionsurveycontact", ConditionOperator.Equal, contactId) }
                }
                    };
                    if (service.RetrieveMultiple(existingQuery).Entities.Any())
                        return Content(HttpStatusCode.Conflict, ApiResponse<object>.Error("Feedback already submitted for this contact."));

                    feedback["new_satisfactionsurveycontact"] = new EntityReference("contact", contactId);
                    linkedVia = "Contact";
                }
                // 🔹 Case 2: Account feedback → copy rep phone into feedback
                else if (!string.IsNullOrWhiteSpace(model.AccountId))
                {
                    Guid accountId = new Guid(model.AccountId);

                    var account = service.Retrieve("account", accountId,
                        new ColumnSet("name", "new_companyrepresentativephonenumber", "new_crnumber", "emailaddress1"));

                    if (account == null)
                        return Content(HttpStatusCode.NotFound, ApiResponse<object>.Error($"Account {accountId} not found."));

                    string repPhone = account.GetAttributeValue<string>("new_companyrepresentativephonenumber");
                    string crNumber = account.GetAttributeValue<string>("new_crnumber");
                    string accName = account.GetAttributeValue<string>("name");

                    // Store these details in text fields on feedback (adjust to your schema)
                    feedback["new_name"] = accName;
                    feedback["new_youropinionmatterstouspleaseshareyourcom"] =
                        $"Representative Phone: {repPhone}, CR Number: {crNumber}";

                    linkedVia = "Account";
                }

                // 🔹 Common fields
                if (model.ServiceSatisfaction >= 1 && model.ServiceSatisfaction <= 5)
                    feedback["new_howsatisfiedareyouwiththeserviceprovideda"] = new OptionSetValue(model.ServiceSatisfaction);

                if (model.StaffEfficiency >= 1 && model.StaffEfficiency <= 5)
                    feedback["new_howsatisfiedareyouwiththeefficiencyofthes"] = new OptionSetValue(model.StaffEfficiency);

                if (model.Reasons != null && model.Reasons.Any())
                    feedback["new_helpusbetterunderstandwhyyouchosetovisitt"] =
                        new OptionSetValueCollection(model.Reasons.Select(r => new OptionSetValue(r)).ToList());

                if (!string.IsNullOrWhiteSpace(model.SpecifyOther))
                    feedback["new_name"] = (feedback.Contains("new_name") ? feedback["new_name"] + " | " : "") + model.SpecifyOther.Trim();

                if (!string.IsNullOrWhiteSpace(model.Opinion))
                    feedback["new_youropinionmatterstouspleaseshareyourcom"] =
                        (feedback.Contains("new_youropinionmatterstouspleaseshareyourcom")
                            ? feedback["new_youropinionmatterstouspleaseshareyourcom"] + " | " + model.Opinion.Trim()
                            : model.Opinion.Trim());

                var feedbackId = service.Create(feedback);

                return Ok(ApiResponse<object>.Success(new
                {
                    FeedbackId = feedbackId,
                    LinkedVia = linkedVia
                }, "Visitor feedback submitted successfully"));
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, ApiResponse<object>.Error($"CRM error: {ex.Message}"));
            }
        }

        [HttpGet]
        [Route("by-visitor-number/{visitorNumber}")]
        public IHttpActionResult GetCustomerByVisitorNumber(string visitorNumber)
        {
            var authHeader = Request.Headers.Authorization;
            string expectedToken = ConfigurationManager.AppSettings["ApiBearerToken"];

            if (authHeader == null || authHeader.Scheme != "Bearer" || authHeader.Parameter != expectedToken)
                return Content(HttpStatusCode.Unauthorized, ApiResponse<object>.Error("Unauthorized - Invalid bearer token"));

            if (string.IsNullOrWhiteSpace(visitorNumber))
                return Content(HttpStatusCode.BadRequest, ApiResponse<object>.Error("Visitor number is required."));

            try
            {
                var service = _crmService.GetService();

                // 🔹 Query visitor by new_visitornumber
                var query = new QueryExpression("new_visitor")
                {
                    ColumnSet = new ColumnSet("new_visitorid", "new_visitornumber", "new_contactname", "new_companyname"),
                    Criteria = new FilterExpression
                    {
                        Conditions =
                {
                    new ConditionExpression("new_visitornumber", ConditionOperator.Equal, visitorNumber)
                }
                    }
                };

                var visitors = service.RetrieveMultiple(query);

                if (visitors.Entities.Count == 0)
                    return Content(HttpStatusCode.NotFound, ApiResponse<object>.Error($"No visitor found with number: {visitorNumber}"));

                var visitor = visitors.Entities.First();
                var visitorId = visitor.Id;

                var contactRef = visitor.GetAttributeValue<EntityReference>("new_contactname");
                var accountRef = visitor.GetAttributeValue<EntityReference>("new_companyname");

                if (contactRef != null)
                {
                    var contact = service.Retrieve("contact", contactRef.Id,
                        new ColumnSet("contactid", "fullname", "firstname", "lastname",
                                      "emailaddress1", "mobilephone", "statuscode", "createdon"));

                    return Ok(ApiResponse<object>.Success(new
                    {
                        VisitorNumber = visitorNumber,
                        VisitorId = visitorId,
                        EntityType = "Contact",
                        ContactId = contact.Id,
                        FullName = contact.GetAttributeValue<string>("fullname"),
                        FirstName = contact.GetAttributeValue<string>("firstname"),
                        LastName = contact.GetAttributeValue<string>("lastname"),
                        Email = contact.GetAttributeValue<string>("emailaddress1"),
                        MobilePhone = contact.GetAttributeValue<string>("mobilephone"),
                        Status = contact.FormattedValues.ContainsKey("statuscode") ? contact.FormattedValues["statuscode"] : null,
                        CreatedOn = contact.GetAttributeValue<DateTime?>("createdon")
                    }, "Contact retrieved successfully"));
                }

                if (accountRef != null)
                {
                    var account = service.Retrieve("account", accountRef.Id,
                        new ColumnSet("accountid", "name", "emailaddress1",
                                      "new_companyrepresentativephonenumber",
                                      "new_crnumber", "statuscode", "createdon"));

                    return Ok(ApiResponse<object>.Success(new
                    {
                        VisitorNumber = visitorNumber,
                        VisitorId = visitorId,
                        EntityType = "Account",
                        AccountId = account.Id,
                        Name = account.GetAttributeValue<string>("name"),
                        Email = account.GetAttributeValue<string>("emailaddress1"),
                        RepresentativePhone = account.GetAttributeValue<string>("new_companyrepresentativephonenumber"),
                        CRNumber = account.GetAttributeValue<string>("new_crnumber"),
                        Status = account.FormattedValues.ContainsKey("statuscode") ? account.FormattedValues["statuscode"] : null,
                        CreatedOn = account.GetAttributeValue<DateTime?>("createdon")
                    }, "Account retrieved successfully"));
                }

                return Content(HttpStatusCode.NotFound, ApiResponse<object>.Error("Visitor has no associated Contact or Account."));
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, ApiResponse<object>.Error($"CRM error: {ex.Message}"));
            }
        }
    }
}

