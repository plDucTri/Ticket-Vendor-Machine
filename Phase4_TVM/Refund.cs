using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class Refund
    {
        public string refundId
        { get; set; }

        public decimal amount
        { get; set; }

        public string reason
        { get; set; }

        public string status
        { get; set; }

        public void request()
        {
            status = "Requested";
        }
    }
}