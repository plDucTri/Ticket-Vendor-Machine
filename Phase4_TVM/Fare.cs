using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class Fare
    {
        public string fareId
        { get; set; }

        public decimal amount
        { get; set; }

        public DateTime effectiveDate
        { get; set; }
    }
}