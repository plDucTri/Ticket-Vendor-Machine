using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class TicketVendorMachine
    {
        public string machineID
        { get; set; }

        public string location
        { get; set; }

        public string status
        { get; set; }

        public TVMScreen TVMScreen
        { get; set; }

        public TVMController TVMController
        { get; set; }

        public TicketPrinter TicketPrinter
        { get; set; }
    }
}