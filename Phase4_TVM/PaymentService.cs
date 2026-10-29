using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class PaymentService
    {
        public PaymentGateway PaymentGateway
        { get; set; }

        public Payment Payment
        { get; set; }

        public void createPayment()
        {
            throw new System.NotImplementedException();
        }

        public void verifyCallback()
        {
            throw new System.NotImplementedException();
        }

        public void reconcileTransaction()
        {
            throw new System.NotImplementedException();
        }
    }
}