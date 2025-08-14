﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Web.Http;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using TicketSystemApi.Services;

namespace TicketSystemApi.Controllers
{
    [Authorize] 
    [RoutePrefix("api/cases")]
    public class CaseReportController : ApiController
    {
        private const string CLAIM_USERNAME = "crm_username";
        private const string CLAIM_PASSWORD = "crm_password";

        [HttpGet]
        [Route("report")]
        public IHttpActionResult GetCases(string filter = "all", int page = 1, int? pageSize = null)
        {
            try
            {
                var identity = (ClaimsIdentity)User.Identity;

                var username = identity.FindFirst(CLAIM_USERNAME)?.Value;
                var password = identity.FindFirst(CLAIM_PASSWORD)?.Value;
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                    return Unauthorized();

                // ✅ Connect AS THE USER who got the token (enforces CRM RBA automatically)
                var service = new CrmService().GetService1(username, password);

                // ---- your existing logic from here down (unchanged) ----

                var query = new QueryExpression("incident")
                {
                    ColumnSet = new ColumnSet(
                        "ticketnumber", "createdon", "modifiedon", "statuscode", "prioritycode",
                        "new_ticketclosuredate", "new_description", "new_ticketsubmissionchannel",
                        "new_businessunitid", "createdby", "modifiedby", "ownerid", "customerid",
                        "new_tickettype", "new_mainclassification", "new_subclassificationitem",
                        "new_isreopened", "new_reopendatetime"
                    ),
                    PageInfo = new PagingInfo
                    {
                        PageNumber = page,
                        Count = (!pageSize.HasValue || pageSize.Value <= 0) ? int.MaxValue : pageSize.Value,
                        PagingCookie = null
                    }
                };

                query.Orders.Add(new OrderExpression("createdon", OrderType.Descending));

                var ksaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
                var ksaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ksaTimeZone);
                DateTime ksaStart;

                if (filter.ToLower() == "daily")
                {
                    ksaStart = ksaNow.Date;
                    query.Criteria.AddCondition("createdon", ConditionOperator.OnOrAfter, ksaStart);
                }
                else if (filter.ToLower() == "weekly")
                {
                    ksaStart = ksaNow.Date.AddDays(-7);
                    query.Criteria.AddCondition("createdon", ConditionOperator.OnOrAfter, ksaStart);
                }
                else if (filter.ToLower() == "monthly")
                {
                    ksaStart = new DateTime(ksaNow.Year, ksaNow.Month, 1);
                    query.Criteria.AddCondition("createdon", ConditionOperator.OnOrAfter, ksaStart);
                }

                var result = service.RetrieveMultiple(query);

                var records = result.Entities.Select(e =>
                {
                    // Fetch the SLA details for the incident
                    var slaDetails = GetSlaDetailsWithTimestamps(service, e.Id);

                    // Fetch the current stage for the incident (unchanged)
                    var currentStage = MapStatusCodeToStage(e);

                    // Extract the SLA statuses from slaDetails and cast to string
                    var assignmentStatus = GetSafeValue(slaDetails, "AssignmentTimeByKPI", "Status") as string;
                    var processingStatus = GetSafeValue(slaDetails, "ProcessingTimeByKPI", "Status") as string;
                    var solutionVStatus = GetSafeValue(slaDetails, "SolutionVerificationTimeByKPI", "Status") as string;

                    // Get the escalation level based on the extracted SLA statuses
                    var escalationLevel = GetEscalationLevelFromStatuses(assignmentStatus, processingStatus, solutionVStatus);

                    // Fetch the Customer Satisfaction (CSAT) data
                    var csat = GetCustomerSatisfactionFeedback(service, e.Id);

                    return new
                    {
                        TicketID = e.GetAttributeValue<string>("ticketnumber"),
                        CreatedBy = e.GetAttributeValue<EntityReference>("createdby")?.Name,
                        AgentName = e.GetAttributeValue<EntityReference>("ownerid")?.Name,
                        CustomerID = e.GetAttributeValue<EntityReference>("customerid")?.Id,
                        CustomerName = e.GetAttributeValue<EntityReference>("customerid")?.Name,
                        CreatedOn = ConvertToKsaTime(e.GetAttributeValue<DateTime?>("createdon"))?.ToString("yyyy-MM-dd HH:mm:ss"),
                        TicketType = e.GetAttributeValue<EntityReference>("new_tickettype")?.Name,
                        Category = e.GetAttributeValue<EntityReference>("new_tickettype")?.Name,
                        SubCategory1 = e.GetAttributeValue<EntityReference>("new_mainclassification")?.Name,
                        SubCategory2 = e.GetAttributeValue<EntityReference>("new_subclassificationitem")?.Name,
                        Status = e.FormattedValues.Contains("statuscode") ? e.FormattedValues["statuscode"] : null,
                        TicketModifiedDateTime = ConvertToKsaTime(e.GetAttributeValue<DateTime?>("modifiedon"))?.ToString("yyyy-MM-dd HH:mm:ss"),
                        Department = e.Attributes.Contains("new_businessunitid") ? ((EntityReference)e["new_businessunitid"]).Name : null,
                        TicketChannel = e.FormattedValues.Contains("new_ticketsubmissionchannel") ? e.FormattedValues["new_ticketsubmissionchannel"] : null,

                        //TotalResolutionTime = CalculateDurationFormatted(e),
                        TotalTicketDuration = CalculateDurationFormatted(e),

                        Description = e.GetAttributeValue<string>("new_description"),
                        ModifiedBy = e.GetAttributeValue<EntityReference>("modifiedby")?.Name,
                        Priority = e.FormattedValues.Contains("prioritycode") ? e.FormattedValues["prioritycode"] : null,
                        // (same as ResolutionDateTime)ClosedOn = ConvertToKsaTime(e.GetAttributeValue<DateTime?>("new_ticketclosuredate")),

                        ResolutionDateTime = GetResolutionDateTime(e)?.ToString("yyyy-MM-dd HH:mm:ss"),

                        // CSAT Fields
                        Customer_Satisfaction_Score = csat.Comment,
                        How_Satisfied_Are_You_With_How_The_Ticket_Was_Handled = csat.Score,
                        Was_the_Time_Taken_to_process_the_ticket_Appropriate = csat.AppropriateTimeTaken,
                        How_can_we_Improve_the_ticket_processing_experience = csat.ImprovementComment,

                        // Other Fields
                        IsReopened = string.IsNullOrWhiteSpace(e.GetAttributeValue<string>("new_isreopened")) ? "No" : e.GetAttributeValue<string>("new_isreopened"),
                        SlaViolation = GetSlaViolationStatus(service, e.Id),

                        // SLA KPI Fields
                        AssignmentTimeByKPI = GetSafeValue(slaDetails, "AssignmentTimeByKPI", "Status"),
                        ProcessingTimeByKPI = GetSafeValue(slaDetails, "ProcessingTimeByKPI", "Status"),
                        SolutionVerificationTimeByKPI = GetSafeValue(slaDetails, "SolutionVerificationTimeByKPI", "Status"),

                        // Escalation Level
                        CurrentStage = currentStage,
                        EscalationLevel = escalationLevel, // Set the escalation level here

                        ReopenedOn = ConvertToKsaTime(e.GetAttributeValue<DateTime?>("new_reopendatetime"))?.ToString("yyyy-MM-dd HH:mm:ss"),

                        // Time Metrics for Assignment, Processing, and Solution Verification KPIs
                        AssignmentWarningTime = GetSafeValue(slaDetails, "AssignmentTimeByKPI", "WarningTime"),
                        AssignmentFailureTime = GetSafeValue(slaDetails, "AssignmentTimeByKPI", "FailureTime"),
                        AssignmentSucceededOn = GetSafeValue(slaDetails, "AssignmentTimeByKPI", "SucceededOn"),

                        ProcessingWarningTime = GetSafeValue(slaDetails, "ProcessingTimeByKPI", "WarningTime"),
                        ProcessingFailureTime = GetSafeValue(slaDetails, "ProcessingTimeByKPI", "FailureTime"),
                        ProcessingSucceededOn = GetSafeValue(slaDetails, "ProcessingTimeByKPI", "SucceededOn"),

                        SolutionVerificationWarningTime = GetSafeValue(slaDetails, "SolutionVerificationTimeByKPI", "WarningTime"),
                        SolutionVerificationFailureTime = GetSafeValue(slaDetails, "SolutionVerificationTimeByKPI", "FailureTime"),
                        SolutionVerificationSucceededOn = GetSafeValue(slaDetails, "SolutionVerificationTimeByKPI", "SucceededOn"),
                    };
                }).ToList();

                return Ok(new { Page = page, PageSize = pageSize, Count = records.Count, Records = records });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Report API failed: {ex.Message}"));
            }
        }
        // ✅ Safe value extractor
        private object GetSafeValue(Dictionary<string, Dictionary<string, object>> dict, string key, string innerKey)
        {
            return dict.ContainsKey(key) && dict[key].ContainsKey(innerKey) ? dict[key][innerKey] : null;
        }
        private DateTime? ConvertToKsaTime(DateTime? utcDate)
        {
            if (!utcDate.HasValue)
                return null;

            TimeZoneInfo ksaZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDate.Value, DateTimeKind.Utc), ksaZone);
        }

        private DateTime? GetResolutionDateTime(Entity incident)
        {
            var resolvedStatusCodes = new HashSet<int> { 5, 6, 100000003, 100000007, 2000 };

            if (incident.Contains("new_ticketclosuredate") && incident["new_ticketclosuredate"] is DateTime closure)
                return ConvertToKsaTime(closure);

            if (incident.Contains("statuscode") && incident["statuscode"] is OptionSetValue status &&
                resolvedStatusCodes.Contains(status.Value) &&
                incident.Contains("modifiedon") && incident["modifiedon"] is DateTime modified)
                return ConvertToKsaTime(modified);

            return null;
        }

        private (int? Score, string Comment, string AppropriateTimeTaken, string ImprovementComment) GetCustomerSatisfactionFeedback(IOrganizationService service, Guid caseId)
        {
            var query = new QueryExpression("new_customersatisfactionscore")
            {
                ColumnSet = new ColumnSet("new_customersatisfactionrating", "new_customersatisfactionscore", "new_wasthetimetakentoprocesstheticketappropri", "new_comment"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("new_csatcase", ConditionOperator.Equal, caseId) }
                }
            };

            var result = service.RetrieveMultiple(query);
            var record = result.Entities.FirstOrDefault();
            if (record == null)
                return (null, null, null, null);

            var score = record.GetAttributeValue<OptionSetValue>("new_customersatisfactionrating")?.Value;
            var comment = record.GetAttributeValue<string>("new_customersatisfactionscore");
            var improvementComment = record.GetAttributeValue<string>("new_comment");

            string appropriateTimeTaken = null;
            if (record.Attributes.Contains("new_wasthetimetakentoprocesstheticketappropri"))
            {
                var timeTaken = record.GetAttributeValue<bool?>("new_wasthetimetakentoprocesstheticketappropri");
                appropriateTimeTaken = timeTaken.HasValue ? (timeTaken.Value ? "Yes" : "No") : null;
            }

            return (score, comment, appropriateTimeTaken, improvementComment);
        }

        private string MapStatusCodeToStage(Entity ticket)
        {
            var statusCode = ticket.GetAttributeValue<OptionSetValue>("statuscode")?.Value;
            switch (statusCode)
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

        private string CalculateDurationFormatted(Entity incident)
        {
            // Convert CreatedOn to KSA
            DateTime? createdOn = ConvertToKsaTime(incident.GetAttributeValue<DateTime?>("createdon"));
            DateTime? resolvedOn = GetResolutionDateTime(incident);

            if (!createdOn.HasValue || !resolvedOn.HasValue)
                return null;

            // Calculate duration
            TimeSpan duration = resolvedOn.Value - createdOn.Value;

            // Format as HH:mm:ss (cumulative hours)
            return string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)duration.TotalHours,
                duration.Minutes,
                duration.Seconds);
        }

        private string GetEscalationLevelFromStatuses(string assignmentStatus, string processingStatus, string solutionVStatus)
        {
            bool a = string.Equals(assignmentStatus, "In Progress", StringComparison.OrdinalIgnoreCase);
            bool p = string.Equals(processingStatus, "In Progress", StringComparison.OrdinalIgnoreCase);
            bool s = string.Equals(solutionVStatus, "In Progress", StringComparison.OrdinalIgnoreCase);

            if (a && p && s) return "Level 1";
            if (!a && p && s) return "Level 2";
            if (!a && !p && s) return "Level 3";
            return "Not Applicable";
        }

        private Dictionary<string, Dictionary<string, object>> GetSlaDetailsWithTimestamps(IOrganizationService service, Guid caseId)
        {
            var query = new QueryExpression("slakpiinstance")
            {
                ColumnSet = new ColumnSet("name", "status", "failuretime", "warningtime", "succeededon"), // ✅ Fixed: removed "regardingobjectid"
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("regarding", ConditionOperator.Equal, caseId) }
                }
            };

            var result = service.RetrieveMultiple(query);
            var ksaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");

            var details = new Dictionary<string, Dictionary<string, object>>();

            foreach (var kpi in result.Entities)
            {
                var rawName = kpi.GetAttributeValue<string>("name");
                if (string.IsNullOrWhiteSpace(rawName)) continue;

                var normalizedKey = rawName.Replace(" ", "").Replace("byKPI", "ByKPI");

                if (!details.ContainsKey(normalizedKey))
                {
                    details[normalizedKey] = new Dictionary<string, object>();
                }

                var status = kpi.FormattedValues.Contains("status") ? kpi.FormattedValues["status"] : null;

                DateTime? failureTime = kpi.GetAttributeValue<DateTime?>("failuretime");
                DateTime? warningTime = kpi.GetAttributeValue<DateTime?>("warningtime");
                DateTime? succeededOn = kpi.GetAttributeValue<DateTime?>("succeededon");

                details[normalizedKey]["Status"] = status;
                details[normalizedKey]["FailureTime"] = failureTime.HasValue
                    ? TimeZoneInfo.ConvertTimeFromUtc(failureTime.Value, ksaTimeZone).ToString("yyyy-MM-dd HH:mm:ss")
                    : null;
                details[normalizedKey]["WarningTime"] = warningTime.HasValue
                    ? TimeZoneInfo.ConvertTimeFromUtc(warningTime.Value, ksaTimeZone).ToString("yyyy-MM-dd HH:mm:ss")
                    : null;
                details[normalizedKey]["SucceededOn"] = succeededOn.HasValue
                    ? TimeZoneInfo.ConvertTimeFromUtc(succeededOn.Value, ksaTimeZone).ToString("yyyy-MM-dd HH:mm:ss")
                    : null;
            }

            return details;
        }

        private string GetSlaViolationStatus(IOrganizationService service, Guid caseId)
        {
            var query = new QueryExpression("slakpiinstance")
            {
                ColumnSet = new ColumnSet("name", "status"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("regarding", ConditionOperator.Equal, caseId) }
                }
            };

            var result = service.RetrieveMultiple(query);

            foreach (var kpi in result.Entities)
            {
                var rawName = kpi.GetAttributeValue<string>("name") ?? "";
                var normalizedName = rawName.Replace(" ", "").Replace("byKPI", "ByKPI");
                var statusFormatted = kpi.FormattedValues.Contains("status") ? kpi.FormattedValues["status"] : null;

                if (!string.IsNullOrEmpty(statusFormatted) &&
                    statusFormatted.ToLower().Contains("noncompliant") &&
                    (
                        normalizedName == "AssignmentTimeByKPI" ||
                        normalizedName == "ProcessingTimeByKPI" ||
                        normalizedName == "SolutionVerificationTimeByKPI"
                    ))
                {
                    return "Yes";
                }
            }

            return "No";
        }
        //  ===== UNUSED CODE BELOW =====



        //private string GetEscalationLevel(Dictionary<string, Dictionary<string, object>> slaStatuses)
        //{
        //    bool isAssignmentInProgress = string.Equals(slaStatuses["AssignmentTimeByKPI"]?["Status"]?.ToString(), "In Progress", StringComparison.OrdinalIgnoreCase);
        //    bool isProcessingInProgress = string.Equals(slaStatuses["ProcessingTimeByKPI"]?["Status"]?.ToString(), "In Progress", StringComparison.OrdinalIgnoreCase);
        //    bool isVerificationInProgress = string.Equals(slaStatuses["SolutionVerificationTimeByKPI"]?["Status"]?.ToString(), "In Progress", StringComparison.OrdinalIgnoreCase);

        //    if (isAssignmentInProgress && isProcessingInProgress && isVerificationInProgress)
        //        return "Level 1";
        //    if (!isAssignmentInProgress && isProcessingInProgress && isVerificationInProgress)
        //        return "Level 2";
        //    if (!isAssignmentInProgress && !isProcessingInProgress && isVerificationInProgress)
        //        return "Level 3";

        //    return null;
        //}

        //private string YesNoFromBool(Entity e, string attr)
        //{
        //    if (!e.Attributes.Contains(attr)) return null;
        //    var val = e[attr];
        //    if (val is bool b) return b ? "Yes" : "No";
        //    if (e.FormattedValues.Contains(attr)) return e.FormattedValues[attr];
        //    return null;
        //}

        //// KPI status (e.g., "In Progress", "Succeeded", "Noncompliant")
        //private string GetKpiStatus(IOrganizationService svc, Guid caseId, string kpiName)
        //{
        //    var qe = new QueryExpression("slakpiinstance")
        //    {
        //        ColumnSet = new ColumnSet("status"),
        //        Criteria = new FilterExpression
        //        {
        //            Conditions =
        //    {
        //        new ConditionExpression("regarding", ConditionOperator.Equal, caseId),
        //        new ConditionExpression("name", ConditionOperator.Equal, kpiName)
        //    }
        //        },
        //        TopCount = 1
        //    };

        //    var rec = svc.RetrieveMultiple(qe).Entities.FirstOrDefault();
        //    if (rec == null) return null;

        //    return rec.FormattedValues.Contains("status")
        //        ? rec.FormattedValues["status"]
        //        : (rec.GetAttributeValue<OptionSetValue>("status") != null
        //            ? rec.GetAttributeValue<OptionSetValue>("status").Value.ToString()
        //            : null);
        //}

        //// KPI warning/failure times (return as tuple; we'll unpack to strings)
        //private (string Warning, string Failure) GetKpiTimes(IOrganizationService svc, Guid caseId, string kpiName)
        //{
        //    var qe = new QueryExpression("slakpiinstance")
        //    {
        //        ColumnSet = new ColumnSet("warningtime", "failuretime"),
        //        Criteria = new FilterExpression
        //        {
        //            Conditions =
        //    {
        //        new ConditionExpression("regarding", ConditionOperator.Equal, caseId),
        //        new ConditionExpression("name", ConditionOperator.Equal, kpiName)
        //    }
        //        },
        //        TopCount = 1
        //    };

        //    var rec = svc.RetrieveMultiple(qe).Entities.FirstOrDefault();
        //    if (rec == null) return (null, null);

        //    return (
        //        ToKsaString(rec.GetAttributeValue<DateTime?>("warningtime")),
        //        ToKsaString(rec.GetAttributeValue<DateTime?>("failuretime"))
        //    );
        //}
        //private string AggregateSlaViolation(Entity e)
        //{
        //    bool l1 = e.GetAttributeValue<bool?>("new_slaviolationl1") ?? false;
        //    bool l2 = e.GetAttributeValue<bool?>("new_slaviolationl2") ?? false;
        //    bool l3 = e.GetAttributeValue<bool?>("new_slaviolationl3") ?? false;
        //    return (l1 || l2 || l3) ? "Yes" : "No";
        //}
        //private int StageIndex(string stage)
        //{
        //    int i;
        //    return StageOrder.TryGetValue(stage ?? "Unknown", out i) ? i : 99;
        //}
        //// C# 7.3-safe explicit new
        //private static readonly Dictionary<string, int> StageOrder =
        //    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        //        {
        //            { "Ticket Creation", 1 },
        //            { "Approval and Forwarding", 2 },
        //            { "Processing- Department", 3 },
        //            { "Processing", 4 },
        //            { "Solution Verification", 5 },
        //            { "Ticket Closure", 6 },
        //            { "Ticket Reopen", 7 },
        //            { "Problem Solved", 8 },
        //            { "Information Provided", 9 },
        //            { "Cancelled", 10 },
        //            { "Merged", 11 },
        //            { "Close", 12 },
        //            { "Unknown", 99 }
        //        };


        //private string ToKsaString(DateTime? utc)
        //{
        //    var v = ConvertToKsaTime(utc);
        //    return v.HasValue ? v.Value.ToString("yyyy-MM-dd HH:mm:ss") : null;
        //}



    }
}
