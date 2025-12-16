using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Web.Http;
using TicketSystemApi.Services;
using TicketSystemApi.Models;

namespace TicketSystemApi.Controllers
{
    [Authorize]
    [RoutePrefix("visitors")]
    public class VisitorsController : ApiController
    {
        private const string CLAIM_USERNAME = "crm_username";
        private const string CLAIM_PASSWORD = "crm_password";

        [HttpGet]
        [Route("")]
        public IHttpActionResult GetVisitors(
            string filter = "all",
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

                DateTime ksaNow = DateTime.UtcNow.AddHours(3);

                // ===================== QUERY =====================
                var query = new QueryExpression("new_visitor")
                {
                    ColumnSet = new ColumnSet(
                        "new_visitorid",
                        "new_visitornumber",
                        "new_contactname",
                        "new_companyname",
                        "new_purposeofvisit",
                        "new_actiontake",
                        "new_category",
                        "new_branch",
                        "createdon",
                        "modifiedon"
                    ),
                    PageInfo = new PagingInfo
                    {
                        PageNumber = page,
                        Count = pageSize,
                        ReturnTotalRecordCount = true
                    }
                };

                ApplyDateFilter(query, filter, ksaNow);

                // ===================== ACCOUNT =====================
                var acc = query.AddLink(
                    "account",
                    "new_companyname",
                    "accountid",
                    JoinOperator.LeftOuter
                );
                acc.Columns = new ColumnSet(
                    "name",
                    "emailaddress1",
                    "new_crnumber",
                    "new_companyrepresentativephonenumber"
                );
                acc.EntityAlias = "acc";

                // ===================== CONTACT =====================
                var con = query.AddLink(
                    "contact",
                    "new_contactname",
                    "contactid",
                    JoinOperator.LeftOuter
                );
                con.Columns = new ColumnSet(
                    "fullname",
                    "emailaddress1",
                    "mobilephone"
                );
                con.EntityAlias = "con";

                // ===================== SURVEY (CHILD) =====================
                var survey = query.AddLink(
                    "new_satisfactionsurveysms",
                    "new_visitorid",
                    "new_satisfactionsurveyvisitor",
                    JoinOperator.LeftOuter
                );

                survey.Columns = new ColumnSet(
                    "new_howsatisfiedareyouwiththeserviceprovideda",
                    "new_howsatisfiedareyouwiththeefficiencyofthes",
                    "new_helpusbetterunderstandwhyyouchosetovisitt",
                    "new_youropinionmatterstouspleaseshareyourcom",
                    "createdon"
                );

                survey.EntityAlias = "survey";
                survey.Orders.Add(new OrderExpression("createdon", OrderType.Descending));

                // ===================== EXECUTE =====================
                var result = service.RetrieveMultiple(query);

                var records = new List<VisitorDto>();

                foreach (var v in result.Entities)
                {
                    // ---------- SAFE SURVEY READ ----------
                    int? serviceSatisfaction = null;
                    int? staffEfficiency = null;
                    List<int> visitReasonValues = null;
                    string visitorComments = null;

                    if (v.Attributes.TryGetValue(
                        "survey.new_howsatisfiedareyouwiththeserviceprovideda",
                        out var ssObj) && ssObj is AliasedValue ssVal && ssVal.Value is int)
                    {
                        serviceSatisfaction = (int)ssVal.Value;
                    }

                    if (v.Attributes.TryGetValue(
                        "survey.new_howsatisfiedareyouwiththeefficiencyofthes",
                        out var seObj) && seObj is AliasedValue seVal && seVal.Value is int)
                    {
                        staffEfficiency = (int)seVal.Value;
                    }

                    if (v.Attributes.TryGetValue(
                        "survey.new_helpusbetterunderstandwhyyouchosetovisitt",
                        out var vrObj) && vrObj is AliasedValue vrVal &&
                        vrVal.Value is OptionSetValueCollection col)
                    {
                        visitReasonValues = col.Select(o => o.Value).ToList();
                    }

                    if (v.Attributes.TryGetValue(
                        "survey.new_youropinionmatterstouspleaseshareyourcom",
                        out var cmObj) && cmObj is AliasedValue cmVal)
                    {
                        visitorComments = cmVal.Value as string;
                    }

                    records.Add(new VisitorDto
                    {
                        VisitorId = v.Id,
                        VisitorNumber = v.GetAttributeValue<string>("new_visitornumber"),

                        AccountId = v.GetAttributeValue<EntityReference>("new_companyname")?.Id,
                        AccountName = GetAliased<string>(v, "acc.name"),
                        AccountEmail = GetAliased<string>(v, "acc.emailaddress1"),
                        CrNumber = GetAliased<string>(v, "acc.new_crnumber"),
                        AccountPhone = GetAliased<string>(v, "acc.new_companyrepresentativephonenumber"),

                        ContactId = v.GetAttributeValue<EntityReference>("new_contactname")?.Id,
                        ContactName = GetAliased<string>(v, "con.fullname"),
                        ContactEmail = GetAliased<string>(v, "con.emailaddress1"),
                        ContactMobile = GetAliased<string>(v, "con.mobilephone"),

                        PurposeOfVisit = v.GetAttributeValue<string>("new_purposeofvisit"),
                        ActionTaken = v.GetAttributeValue<string>("new_actiontake"),

                        CategoryValue = v.GetAttributeValue<OptionSetValue>("new_category")?.Value,
                        Category = v.FormattedValues.Contains("new_category")
                            ? v.FormattedValues["new_category"]
                            : null,

                        BranchValue = v.GetAttributeValue<OptionSetValue>("new_branch")?.Value,
                        Branch = v.FormattedValues.Contains("new_branch")
                            ? v.FormattedValues["new_branch"]
                            : null,

                        CreatedOn = v.GetAttributeValue<DateTime>("createdon"),

                        ServiceSatisfaction = serviceSatisfaction,
                        StaffEfficiency = staffEfficiency,
                        VisitReasonValues = visitReasonValues,
                        VisitReasonLabels = v.FormattedValues
                            .Where(f => f.Key.StartsWith(
                                "survey.new_helpusbetterunderstandwhyyouchosetovisitt"))
                            .Select(f => f.Value)
                            .ToList(),
                        VisitorComments = visitorComments
                    });
                }

                return Ok(new
                {
                    Filter = filter,
                    Page = page,
                    PageSize = pageSize,
                    TotalRecords = result.TotalRecordCount,
                    TotalPages = (int)Math.Ceiling(
                        (double)result.TotalRecordCount / pageSize
                    ),
                    Records = records
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(
                    new Exception($"Visitors API failed: {ex.Message}")
                );
            }
        }

        // ===================== HELPERS =====================
        private static T GetAliased<T>(Entity e, string key)
        {
            return e.Attributes.Contains(key) && e[key] is AliasedValue av
                ? (T)av.Value
                : default;
        }

        private static void ApplyDateFilter(
            QueryExpression query,
            string filter,
            DateTime ksaNow)
        {
            if (string.IsNullOrWhiteSpace(filter)) return;

            filter = filter.ToLower();

            if (filter == "daily")
                query.Criteria.AddCondition("modifiedon", ConditionOperator.OnOrAfter, ksaNow.Date);
            else if (filter == "weekly")
                query.Criteria.AddCondition("modifiedon", ConditionOperator.OnOrAfter, ksaNow.Date.AddDays(-7));
            else if (filter == "monthly")
                query.Criteria.AddCondition("modifiedon", ConditionOperator.OnOrAfter,
                    new DateTime(ksaNow.Year, ksaNow.Month, 1));
            else if (filter == "yesterday")
                query.Criteria.AddCondition("modifiedon", ConditionOperator.Between,
                    new object[] { ksaNow.Date.AddDays(-1), ksaNow.Date.AddSeconds(-1) });
        }
    }
}
