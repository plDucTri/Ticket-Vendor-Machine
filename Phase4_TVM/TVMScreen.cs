using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class TVMScreen
    {
        public List<string> displayDestinations(List<Destination> destinations)
        {
            return destinations.Select(d => d.name.PadRight(28) + displayFare(d.Fare)).ToList();
        }

        public string displayFare(Fare fare)
        {
            return fare.amount.ToString("N0") + " VND";
        }

        public string displayQRCode(QRPayment payment)
        {
            return payment.generateQRCode();
        }

        public string displayMessage(string english, string vietnamese)
        {
            return english + "\n\n" + vietnamese;
        }
    }
}