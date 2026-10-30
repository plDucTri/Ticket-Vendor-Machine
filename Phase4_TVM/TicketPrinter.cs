using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class TicketPrinter
    {
        public TicketPrinter()
        {
            printerId = "PRN-01";
            paperLevel = 100;
        }

        public string printerId
        { get; set; }

        public int paperLevel
        { get; set; }

        public Ticket Ticket
        { get; set; }

        public bool printTicket(Ticket ticket)
        {
            if (!checkStatus() || isPaperLow())
            {
                ticket.status = TicketStatus.PrintFailed;
                return false;
            }
            ticket.status = TicketStatus.Printed;
            paperLevel -= 1;
            return true;
        }

        public bool checkStatus()
        {
            return paperLevel > 0;
        }

        public bool isPaperLow()
        {
            return paperLevel < 10;
        }
    }
}