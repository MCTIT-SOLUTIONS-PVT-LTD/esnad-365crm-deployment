using System;
using System.Collections.Generic;

namespace TicketSystemApi.Models
{
    public class CustomerWithTicketsDto
    {
        // Common
        public Guid CustomerId { get; set; }
        public string CustomerType { get; set; } // Account | Contact

        // Account / Contact Info
        public string Name { get; set; }
        public string Email { get; set; }
        public string MobileOrPhone { get; set; }
        public string CrNumber { get; set; }

        // Related Tickets
        public List<TicketSummaryDto> Tickets { get; set; } = new List<TicketSummaryDto>();
    }

    public class TicketSummaryDto
    {
        public string TicketNumber { get; set; }
        public string Title { get; set; }
    }
}
