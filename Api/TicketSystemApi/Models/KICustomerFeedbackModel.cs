using System.Collections.Generic;

namespace TicketSystemApi.Models
{
    public class KICustomerFeedbackModel
    {
        public string CaseId { get; set; }               // optional GUID string for incident
        public string TicketNumber { get; set; }         // e.g. "202501" or "KI-202501"
        public string CustomerId { get; set; }           // optional
        public string CustomerLogicalName { get; set; }  // "contact" or "account"
        public Dictionary<string, int> Ratings { get; set; } // { "new_overallhowsatisfied..." : 5, ... }
        public int TimeAppropriate { get; set; }         // 1/2 or 0
        public string Comment { get; set; }
        public string Lang { get; set; }                 // "en" or "ar"
    }
}
