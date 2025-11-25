using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TicketSystemApi.Models
{
    public class KICustomerFeedbackModel
    {
        public string CaseId { get; set; }           // optional GUID string for incident
        public string TicketNumber { get; set; }     // e.g. KI-202501
        public string CustomerId { get; set; }       // optional
        public string CustomerLogicalName { get; set; } // "contact" or "account"
        public Dictionary<string, int> Ratings { get; set; } // { "new_overallsatisfactionrating": 5, ...}
        public int TimeAppropriate { get; set; }     // 1/2
        public string Comment { get; set; }
        public string Lang { get; set; }
    }
}