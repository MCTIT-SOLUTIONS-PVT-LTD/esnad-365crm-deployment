using System;
using System.Collections.Generic;
using System.Linq;
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
        [HttpGet]
        [Route("report")]
        public IHttpActionResult GetCases(string filter = "all", int page = 1, int? pageSize = null)
        {
            try
            {
                var identity = (System.Security.Claims.ClaimsIdentity)User.Identity;
                var username = identity.FindFirst("crm_username")?.Value;
                var password = identity.FindFirst("crm_password")?.Value;

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                    return Unauthorized();

                // Use dynamic user credentials (OAuth-based) for the report API
                var service = new CrmService().GetService1(username, password); // Pass dynamic credentials to GetService
                // Pagination & Filter Logic
                var ksaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
                var ksaNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ksaTimeZone);
                DateTime? filterStart = null;

                if (filter.Equals("daily", StringComparison.OrdinalIgnoreCase))
                    filterStart = ksaNow.Date;
                else if (filter.Equals("weekly", StringComparison.OrdinalIgnoreCase))
                    filterStart = ksaNow.Date.AddDays(-7);
                else if (filter.Equals("monthly", StringComparison.OrdinalIgnoreCase))
                    filterStart = new DateTime(ksaNow.Year, ksaNow.Month, 1);

                string dateFilter = filterStart.HasValue
                    ? $"<condition attribute='createdon' operator='on-or-after' value='{filterStart.Value:yyyy-MM-dd}' />"
                    : "";
                // Build FetchXML to retrieve formatted values and names
                string fetchXml = $@"
                  <fetch mapping='logical' version='1.0' page='{page}' count='{pageSize ?? 50}'>
                    <entity name='incident'>
                      <attribute name='ticketnumber'/>
                      <attribute name='createdon'/>
                      <attribute name='modifiedon'/>
                      <attribute name='statuscode'/>
                      <attribute name='prioritycode'/>
                      <attribute name='new_ticketclosuredate'/>
                      <attribute name='new_description'/>
                      <attribute name='new_ticketsubmissionchannel'/>
                      <attribute name='new_businessunitid'/>
                      <attribute name='createdby'/>
                      <attribute name='modifiedby'/>
                      <attribute name='ownerid'/>
                      <attribute name='customerid'/>
                      <attribute name='new_tickettype'/>
                      <attribute name='new_mainclassification'/>
                      <attribute name='new_subclassificationitem'/>
                      <attribute name='new_isreopened'/>
                      <attribute name='new_reopendatetime'/>

                      <!-- NEW: succeeded-on timestamps stored on incident -->
                      <attribute name='new_assignmentsucceededon'/>
                      <attribute name='new_processingsucceededon'/>
                      <attribute name='new_solutionverificationsucceededon'/>

                      <!-- NEW: level-wise SLA violation flags on incident -->
                      <attribute name='new_slaviolationl1'/>
                      <attribute name='new_slaviolationl2'/>
                      <attribute name='new_slaviolationl3'/>

                      <order attribute='createdon' descending='true'/>
                      <filter type='and'>
                        {dateFilter}
                      </filter>
                    </entity>
                  </fetch>";

                // Retrieve cases respecting user security roles
                var result = service.RetrieveMultiple(new FetchExpression(fetchXml));

                var records = result.Entities.Select(e =>
                {
                    var currentStage = MapStatusCodeToStage(e);
                    var s = StageIndex(currentStage);

                    // Gates
                    bool allowAssignment = s >= StageIndex("Ticket Creation");
                    bool allowProcessing = s >= StageIndex("Processing- Department");
                    bool allowSolutionV = s >= StageIndex("Processing");

                    // KPI statuses
                    string assignmentStatus = null, processingStatus = null, solutionVStatus = null;

                    // KPI times (strings; avoids tuple-name issues)
                    string assignmentWarningTime = null, assignmentFailureTime = null;
                    string processingWarningTime = null, processingFailureTime = null;
                    string solutionWarningTime = null, solutionFailureTime = null;

                    if (allowAssignment)
                    {
                        assignmentStatus = GetKpiStatus(service, e.Id, "Assignment Time by KPI");
                        var t = GetKpiTimes(service, e.Id, "Assignment Time by KPI");
                        assignmentWarningTime = t.Warning;
                        assignmentFailureTime = t.Failure;
                    }
                    if (allowProcessing)
                    {
                        processingStatus = GetKpiStatus(service, e.Id, "Processing Time by KPI");
                        var t = GetKpiTimes(service, e.Id, "Processing Time by KPI");
                        processingWarningTime = t.Warning;
                        processingFailureTime = t.Failure;
                    }
                    if (allowSolutionV)
                    {
                        solutionVStatus = GetKpiStatus(service, e.Id, "Solution Verification Time by KPI");
                        var t = GetKpiTimes(service, e.Id, "Solution Verification Time by KPI");
                        solutionWarningTime = t.Warning;
                        solutionFailureTime = t.Failure;
                    }

                    var csat = GetCustomerSatisfactionFeedback(service, e.Id);

                    return new
                    {
                        TicketID = e.GetAttributeValue<string>("ticketnumber"),
                        CreatedBy = e.FormattedValues.Contains("createdby") ? e.FormattedValues["createdby"] : null,
                        AgentName = e.FormattedValues.Contains("ownerid") ? e.FormattedValues["ownerid"] : null,
                        CustomerID = e.GetAttributeValue<EntityReference>("customerid")?.Id,
                        CustomerName = e.FormattedValues.Contains("customerid") ? e.FormattedValues["customerid"] : null,
                        CreatedOn = ToKsaString(e.GetAttributeValue<DateTime?>("createdon")),
                        TicketType = e.FormattedValues.Contains("new_tickettype") ? e.FormattedValues["new_tickettype"] : null,
                        Category = e.FormattedValues.Contains("new_tickettype") ? e.FormattedValues["new_tickettype"] : null,
                        SubCategory1 = e.FormattedValues.Contains("new_mainclassification") ? e.FormattedValues["new_mainclassification"] : null,
                        SubCategory2 = e.FormattedValues.Contains("new_subclassificationitem") ? e.FormattedValues["new_subclassificationitem"] : null,
                        Status = e.FormattedValues.Contains("statuscode") ? e.FormattedValues["statuscode"] : null,
                        TicketModifiedDateTime = ToKsaString(e.GetAttributeValue<DateTime?>("modifiedon")),
                        Department = e.FormattedValues.Contains("new_businessunitid") ? e.FormattedValues["new_businessunitid"] : null,
                        TicketChannel = e.FormattedValues.Contains("new_ticketsubmissionchannel") ? e.FormattedValues["new_ticketsubmissionchannel"] : null,

                        TotalResolutionTime = CalculateDurationFormatted(e),
                        Description = e.GetAttributeValue<string>("new_description"),
                        ModifiedBy = e.FormattedValues.Contains("modifiedby") ? e.FormattedValues["modifiedby"] : null,
                        Priority = e.FormattedValues.Contains("prioritycode") ? e.FormattedValues["prioritycode"] : null,
                        ResolutionDateTime = ToKsaString(GetResolutionDateTime(e)),

                        // CSAT: score = number, comment = text
                        Customer_Satisfaction_Score = csat.Score,
                        How_Satisfied_Are_You_With_How_The_Ticket_Was_Handled = csat.Comment,
                        Was_the_Time_Taken_to_process_the_ticket_Appropriate = csat.AppropriateTimeTaken,
                        How_can_we_Improve_the_ticket_processing_experience = csat.ImprovementComment,

                        IsReopened = string.IsNullOrWhiteSpace(e.GetAttributeValue<string>("new_isreopened")) ? "No" : e.GetAttributeValue<string>("new_isreopened"),

                        // NEW: aggregated Yes/No from incident L1/L2/L3 flags, 
                        SlaViolation = AggregateSlaViolation(e),

                        // KPI statuses (stage-gated)
                        AssignmentTimeByKPI = assignmentStatus,
                        ProcessingTimeByKPI = processingStatus,
                        SolutionVerificationTimeByKPI = solutionVStatus,

                        CurrentStage = currentStage,

                        // Escalation level derived from statuses (optional; returns null if pattern not matched)
                        EscalationLevel = GetEscalationLevelFromStatuses(assignmentStatus, processingStatus, solutionVStatus),

                        ReopenedOn = ToKsaString(e.GetAttributeValue<DateTime?>("new_reopendatetime")),

                        // KPI times (stage-gated)
                        AssignmentWarningTime = assignmentWarningTime,
                        AssignmentFailureTime = assignmentFailureTime,
                        // SucceededOn now stored on incident
                        AssignmentSucceededOn = ToKsaString(e.GetAttributeValue<DateTime?>("new_assignmentsucceededon")),

                        ProcessingWarningTime = processingWarningTime,
                        ProcessingFailureTime = processingFailureTime,
                        ProcessingSucceededOn = ToKsaString(e.GetAttributeValue<DateTime?>("new_processingsucceededon")),

                        SolutionVerificationWarningTime = solutionWarningTime,
                        SolutionVerificationFailureTime = solutionFailureTime,
                        SolutionVerificationSucceededOn = ToKsaString(e.GetAttributeValue<DateTime?>("new_solutionverificationsucceededon")),

                        // Level-wise SLA violation flags now on Incident
                        SlaViolationL1 = YesNoFromBool(e, "new_slaviolationl1"),
                        SlaViolationL2 = YesNoFromBool(e, "new_slaviolationl2"),
                        SlaViolationL3 = YesNoFromBool(e, "new_slaviolationl3"),
                    };
                }).ToList();

                return Ok(new { Page = page, PageSize = pageSize ?? 50, Count = records.Count, Records = records });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception($"Report API failed: {ex.Message}"));
            }
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
        // C# 7.3-safe explicit new
        private static readonly Dictionary<string, int> StageOrder =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Ticket Creation", 1 },
                    { "Approval and Forwarding", 2 },
                    { "Processing- Department", 3 },
                    { "Processing", 4 },
                    { "Solution Verification", 5 },
                    { "Ticket Closure", 6 },
                    { "Ticket Reopen", 7 },
                    { "Problem Solved", 8 },
                    { "Information Provided", 9 },
                    { "Cancelled", 10 },
                    { "Merged", 11 },
                    { "Close", 12 },
                    { "Unknown", 99 }
                };

        private int StageIndex(string stage)
        {
            int i;
            return StageOrder.TryGetValue(stage ?? "Unknown", out i) ? i : 99;
        }

        private string ToKsaString(DateTime? utc)
        {
            var v = ConvertToKsaTime(utc);
            return v.HasValue ? v.Value.ToString("yyyy-MM-dd HH:mm:ss") : null;
        }

        private string YesNoFromBool(Entity e, string attr)
        {
            if (!e.Attributes.Contains(attr)) return null;
            var val = e[attr];
            if (val is bool b) return b ? "Yes" : "No";
            if (e.FormattedValues.Contains(attr)) return e.FormattedValues[attr];
            return null;
        }

        // KPI status (e.g., "In Progress", "Succeeded", "Noncompliant")
        private string GetKpiStatus(IOrganizationService svc, Guid caseId, string kpiName)
        {
            var qe = new QueryExpression("slakpiinstance")
            {
                ColumnSet = new ColumnSet("status"),
                Criteria = new FilterExpression
                {
                    Conditions =
            {
                new ConditionExpression("regarding", ConditionOperator.Equal, caseId),
                new ConditionExpression("name", ConditionOperator.Equal, kpiName)
            }
                },
                TopCount = 1
            };

            var rec = svc.RetrieveMultiple(qe).Entities.FirstOrDefault();
            if (rec == null) return null;

            return rec.FormattedValues.Contains("status")
                ? rec.FormattedValues["status"]
                : (rec.GetAttributeValue<OptionSetValue>("status") != null
                    ? rec.GetAttributeValue<OptionSetValue>("status").Value.ToString()
                    : null);
        }

        // KPI warning/failure times (return as tuple; we'll unpack to strings)
        private (string Warning, string Failure) GetKpiTimes(IOrganizationService svc, Guid caseId, string kpiName)
        {
            var qe = new QueryExpression("slakpiinstance")
            {
                ColumnSet = new ColumnSet("warningtime", "failuretime"),
                Criteria = new FilterExpression
                {
                    Conditions =
            {
                new ConditionExpression("regarding", ConditionOperator.Equal, caseId),
                new ConditionExpression("name", ConditionOperator.Equal, kpiName)
            }
                },
                TopCount = 1
            };

            var rec = svc.RetrieveMultiple(qe).Entities.FirstOrDefault();
            if (rec == null) return (null, null);

            return (
                ToKsaString(rec.GetAttributeValue<DateTime?>("warningtime")),
                ToKsaString(rec.GetAttributeValue<DateTime?>("failuretime"))
            );
        }
        private string AggregateSlaViolation(Entity e)
        {
            bool l1 = e.GetAttributeValue<bool?>("new_slaviolationl1") ?? false;
            bool l2 = e.GetAttributeValue<bool?>("new_slaviolationl2") ?? false;
            bool l3 = e.GetAttributeValue<bool?>("new_slaviolationl3") ?? false;
            return (l1 || l2 || l3) ? "Yes" : "No";
        }

        private string GetEscalationLevelFromStatuses(string assignmentStatus, string processingStatus, string solutionVStatus)
        {
            bool a = string.Equals(assignmentStatus, "In Progress", StringComparison.OrdinalIgnoreCase);
            bool p = string.Equals(processingStatus, "In Progress", StringComparison.OrdinalIgnoreCase);
            bool s = string.Equals(solutionVStatus, "In Progress", StringComparison.OrdinalIgnoreCase);

            if (a && p && s) return "Level 1";
            if (!a && p && s) return "Level 2";
            if (!a && !p && s) return "Level 3";
            return null;
        }
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

        //private Dictionary<string, Dictionary<string, object>> GetSlaDetailsWithTimestamps(IOrganizationService service, Guid caseId)
        //{
        //    var query = new QueryExpression("slakpiinstance")
        //    {
        //        ColumnSet = new ColumnSet("name", "status", "failuretime", "warningtime", "succeededon"), // ✅ Fixed: removed "regardingobjectid"
        //        Criteria = new FilterExpression
        //        {
        //            Conditions = { new ConditionExpression("regarding", ConditionOperator.Equal, caseId) }
        //        }
        //    };

        //    var result = service.RetrieveMultiple(query);
        //    var ksaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");

        //    var details = new Dictionary<string, Dictionary<string, object>>();

        //    foreach (var kpi in result.Entities)
        //    {
        //        var rawName = kpi.GetAttributeValue<string>("name");
        //        if (string.IsNullOrWhiteSpace(rawName)) continue;

        //        var normalizedKey = rawName.Replace(" ", "").Replace("byKPI", "ByKPI");

        //        if (!details.ContainsKey(normalizedKey))
        //        {
        //            details[normalizedKey] = new Dictionary<string, object>();
        //        }

        //        var status = kpi.FormattedValues.Contains("status") ? kpi.FormattedValues["status"] : null;

        //        DateTime? failureTime = kpi.GetAttributeValue<DateTime?>("failuretime");
        //        DateTime? warningTime = kpi.GetAttributeValue<DateTime?>("warningtime");
        //        DateTime? succeededOn = kpi.GetAttributeValue<DateTime?>("succeededon");

        //        details[normalizedKey]["Status"] = status;
        //        details[normalizedKey]["FailureTime"] = failureTime.HasValue
        //            ? TimeZoneInfo.ConvertTimeFromUtc(failureTime.Value, ksaTimeZone).ToString("yyyy-MM-dd HH:mm:ss")
        //            : null;
        //        details[normalizedKey]["WarningTime"] = warningTime.HasValue
        //            ? TimeZoneInfo.ConvertTimeFromUtc(warningTime.Value, ksaTimeZone).ToString("yyyy-MM-dd HH:mm:ss")
        //            : null;
        //        details[normalizedKey]["SucceededOn"] = succeededOn.HasValue
        //            ? TimeZoneInfo.ConvertTimeFromUtc(succeededOn.Value, ksaTimeZone).ToString("yyyy-MM-dd HH:mm:ss")
        //            : null;
        //    }

        //    return details;
        //}

        //private string GetSlaViolationStatus(IOrganizationService service, Guid caseId)
        //{
        //    var query = new QueryExpression("slakpiinstance")
        //    {
        //        ColumnSet = new ColumnSet("name", "status"),
        //        Criteria = new FilterExpression
        //        {
        //            Conditions = { new ConditionExpression("regarding", ConditionOperator.Equal, caseId) }
        //        }
        //    };

        //    var result = service.RetrieveMultiple(query);

        //    foreach (var kpi in result.Entities)
        //    {
        //        var rawName = kpi.GetAttributeValue<string>("name") ?? "";
        //        var normalizedName = rawName.Replace(" ", "").Replace("byKPI", "ByKPI");
        //        var statusFormatted = kpi.FormattedValues.Contains("status") ? kpi.FormattedValues["status"] : null;

        //        if (!string.IsNullOrEmpty(statusFormatted) &&
        //            statusFormatted.ToLower().Contains("noncompliant") &&
        //            (
        //                normalizedName == "AssignmentTimeByKPI" ||
        //                normalizedName == "ProcessingTimeByKPI" ||
        //                normalizedName == "SolutionVerificationTimeByKPI"
        //            ))
        //        {
        //            return "Yes";
        //        }
        //    }

        //    return "No";
        //}

    }
}
