using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TVM
{
    public interface IPaymentGateway
    {
        void requestPayment();
        void queryStatus();
        void refund();
    }
}
