using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class Ticket
    {
        public string ticketId
        { get; set; }

        public string barcode
        { get; set; }

        public DateTime issuedAt
        { get; set; }

        public TicketStatus status { get; set; }

        public string generateBarcode()
        {
            barcode = "TVM-" + ticketId;
            return barcode;
        }
    }
}