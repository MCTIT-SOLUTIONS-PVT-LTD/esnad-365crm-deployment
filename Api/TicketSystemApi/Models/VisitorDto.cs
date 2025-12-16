using System;
using System.Collections.Generic;

namespace TicketSystemApi.Models
{
    public class VisitorDto
    {
        public Guid VisitorId { get; set; }
        public string VisitorNumber { get; set; }

        public Guid? AccountId { get; set; }
        public string AccountName { get; set; }
        public string AccountEmail { get; set; }
        public string CrNumber { get; set; }
        public string AccountPhone { get; set; }

        public Guid? ContactId { get; set; }
        public string ContactName { get; set; }
        public string ContactEmail { get; set; }
        public string ContactMobile { get; set; }

        public string PurposeOfVisit { get; set; }
        public string ActionTaken { get; set; }

        public int? CategoryValue { get; set; }
        public string Category { get; set; }

        public int? BranchValue { get; set; }
        public string Branch { get; set; }

        public DateTime CreatedOn { get; set; }

        // ===================== SURVEY =====================
        public int? ServiceSatisfaction { get; set; }
        public int? StaffEfficiency { get; set; }

        public List<int> VisitReasonValues { get; set; }
        public List<string> VisitReasonLabels { get; set; }

        public string VisitorComments { get; set; }
    }
}
