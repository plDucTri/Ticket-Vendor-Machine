using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class CardPayment : Payment
    {
        public string maskedCardNumber
        { get; set; }

        public string authorizationCode
        { get; set; }
    }
}