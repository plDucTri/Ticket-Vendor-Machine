using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class QRPayment : Payment
    {
        public string qrPayload
        { get; set; }

        public DateTime expiresAt
        { get; set; }

        public string generateQRCode()
        {
            return qrPayload;
        }

        public bool isExpired()
        {
            return DateTime.Now > expiresAt;
        }
    }
}