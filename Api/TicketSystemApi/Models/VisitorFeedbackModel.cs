using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TicketSystemApi.Models
{
    public class VisitorFeedbackModel
    {
        public string VisitorId { get; set; }
        public string TicketId { get; set; }
        public string ContactId { get; set; }   // optional
        public string AccountId { get; set; }
        public int ServiceSatisfaction { get; set; }
        public int StaffEfficiency { get; set; }
        public List<int> Reasons { get; set; }
        public string SpecifyOther { get; set; }
        public string Opinion { get; set; }
    }
}
