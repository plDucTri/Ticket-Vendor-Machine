using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TVM.PaymentGateway;

namespace TVM
{
    public interface IPaymentGateway
    {
        GatewayResponse requestPayment(Payment payment);
        GatewayResponse queryStatus(string providerRef);
        bool refund(string providerRef, decimal amount);
    }
}