using System;
using System.Collections.Generic;

namespace TicketSystemApi.Models
{
    public class CustomerWithTicketsDto
    {
        public Guid CompanyId { get; set; }
        public string CompanyType { get; set; } // Account | Contact
        public string CompanyName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string CrNumber { get; set; }
        public List<TicketSummaryDto> Tickets { get; set; } = new List<TicketSummaryDto>();
    }

    public class TicketSummaryDto
    {
        public string TicketNumber { get; set; }
        public string Title { get; set; }
    }
}
