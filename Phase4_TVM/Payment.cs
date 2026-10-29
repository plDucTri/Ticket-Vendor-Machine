using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public abstract class Payment
    {
        public string paymentId
        { get; set; }

        public decimal amount
        { get; set; }

        public PaymentStatus status
        { get; set; }

        public string providerRef
        { get; set; }

        public List<TVM.Refund> Refund
        { get; set; }

        public void updateStatus(PaymentStatus newStatus)
        {
            status = newStatus;
        }
    }
}