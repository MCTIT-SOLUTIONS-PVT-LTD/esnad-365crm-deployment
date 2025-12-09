using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TicketSystemApi.Models
{
    public class KeyInvestorFeedbackModel
    {
        // GUID of the new_keyinvestorscommunication record (required)
        public string ReferenceRecordId { get; set; }

        // Either ContactId or AccountId (GUID strings)
        public string ContactId { get; set; }
        public string AccountId { get; set; }

        // Ratings expected 1..5
        public int OverallSatisfaction { get; set; }   // maps -> new_overallhowsatisfiedareyouwithyourexperien
        public int Responsiveness { get; set; }        // maps -> new_howsatisfiedareyouwiththeresponsivenessof
        public int Professionalism { get; set; }       // maps -> new_howsatisfiedareyouwiththeprofessionalismo
        public int SolutionProvided { get; set; }      // maps -> new_howsatisfiedareyouwiththesolutionprovided

        // Free text
        public string Comments { get; set; }

        // Optional OwnerId (GUID)
        public string OwnerId { get; set; }
    }
}