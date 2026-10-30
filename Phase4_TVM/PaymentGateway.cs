using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace TVM
{
    public class GatewayResponse
    {
        public string ProviderRef { get; set; }
        public PaymentStatus Status { get; set; }
        public decimal Amount { get; set; }
        public string AuthCode { get; set; }
        public string Signature { get; set; }
    }

    public class PaymentGateway : IPaymentGateway
    {
        private const string Secret = "TVM-DEMO-SHARED-SECRET";

        private class Record
        {
            public PaymentStatus Status;
            public decimal Amount;
            public string AuthCode;
            public bool Refunded;
        }

        private static readonly Dictionary<string, Record> store = new Dictionary<string, Record>();
        private static readonly Random rnd = new Random();

        public GatewayResponse requestPayment(Payment payment)
        {
            string providerRef = "PRV" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper();
            var rec = new Record { Amount = payment.amount, Status = PaymentStatus.Pending };

            var card = payment as CardPayment;
            if (card != null)
            {
                Thread.Sleep(600);
                string last4 = card.maskedCardNumber.Substring(card.maskedCardNumber.Length - 4);
                if (last4 == "0002") rec.Status = PaymentStatus.Failed;
                else if (last4 == "0010") rec.Status = PaymentStatus.TimedOut;
                else
                {
                    rec.Status = PaymentStatus.Verified;
                    rec.AuthCode = "AUTH" + rnd.Next(100000, 999999);
                }
            }

            lock (store) { store[providerRef] = rec; }
            return Build(providerRef, rec);
        }

        public GatewayResponse queryStatus(string providerRef)
        {
            lock (store)
            {
                Record rec;
                if (providerRef == null || !store.TryGetValue(providerRef, out rec))
                    return null;
                return Build(providerRef, rec);
            }
        }

        public bool refund(string providerRef, decimal amount)
        {
            lock (store)
            {
                Record rec;
                if (providerRef == null || !store.TryGetValue(providerRef, out rec)) return false;
                if (rec.Status != PaymentStatus.Verified || rec.Refunded || amount > rec.Amount) return false;
                rec.Refunded = true;
                return true;
            }
        }

        public void simulateScan(string providerRef)
        {
            lock (store)
            {
                Record rec;
                if (providerRef != null && store.TryGetValue(providerRef, out rec) && rec.Status == PaymentStatus.Pending)
                {
                    rec.Status = PaymentStatus.Verified;
                    rec.AuthCode = "QR" + rnd.Next(100000, 999999);
                }
            }
        }

        public string sign(string providerRef, decimal amount, PaymentStatus status)
        {
            using (var sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(
                    providerRef + "|" + amount.ToString("0") + "|" + status + "|" + Secret));
                return BitConverter.ToString(h, 0, 8).Replace("-", "");
            }
        }

        private GatewayResponse Build(string providerRef, Record rec)
        {
            return new GatewayResponse
            {
                ProviderRef = providerRef,
                Status = rec.Status,
                Amount = rec.Amount,
                AuthCode = rec.AuthCode,
                Signature = sign(providerRef, rec.Amount, rec.Status)
            };
        }
    }
}