using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TVM
{
    public class PaymentService
    {
        public const int PaymentTimeoutSeconds = 60;

        public PaymentGateway PaymentGateway
        { get; set; }

        public Payment Payment
        { get; set; }

        public PaymentService()
        {
            PaymentGateway = new PaymentGateway();
        }

        public Payment createPayment(string method, decimal amount, string cardNumber = null, string expiry = null, string cvv = null)
        {
            Payment p;
            if (method == "Card")
            {
                string digits = (cardNumber ?? "").Replace(" ", "").Replace("-", "");
                if (digits.Length < 13 || digits.Length > 19 || !digits.All(char.IsDigit))
                    throw new ArgumentException("Card number must have 13-19 digits.\nSố thẻ phải có 13-19 chữ số.");
                if (!Luhn(digits))
                    throw new ArgumentException("Invalid card number.\nSố thẻ không hợp lệ.");
                if (!ExpiryOk(expiry))
                    throw new ArgumentException("Card expired or wrong format (use MM/yy).\nThẻ hết hạn hoặc sai định dạng (MM/yy).");
                if (string.IsNullOrEmpty(cvv) || cvv.Length < 3 || cvv.Length > 4 || !cvv.All(char.IsDigit))
                    throw new ArgumentException("CVV must have 3-4 digits.\nCVV phải có 3-4 chữ số.");

                p = new CardPayment { maskedCardNumber = "**** **** **** " + digits.Substring(digits.Length - 4) };
            }
            else if (method == "QR")
            {
                p = new QRPayment { expiresAt = DateTime.Now.AddSeconds(PaymentTimeoutSeconds) };
            }
            else
            {
                throw new ArgumentException("Unknown payment method.\nPhương thức thanh toán không hợp lệ.");
            }

            p.paymentId = "PAY" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            p.amount = amount;
            p.status = PaymentStatus.Initiated;
            p.Refund = new List<Refund>();
            Payment = p;

            GatewayResponse resp = PaymentGateway.requestPayment(p);
            if (!verifyCallback(resp, p))
                p.updateStatus(PaymentStatus.Failed);

            var qr = p as QRPayment;
            if (qr != null && p.providerRef != null)
                qr.qrPayload = "TVMPAY|" + p.providerRef + "|" + amount.ToString("0") + "VND";

            return p;
        }

        public bool verifyCallback(GatewayResponse r, Payment p)
        {
            if (r == null || r.ProviderRef == null || p == null) return false;
            if (r.Amount != p.amount) return false;
            if (r.Signature != PaymentGateway.sign(r.ProviderRef, r.Amount, r.Status)) return false;

            p.providerRef = r.ProviderRef;
            p.updateStatus(r.Status);
            var card = p as CardPayment;
            if (card != null) card.authorizationCode = r.AuthCode;
            return true;
        }

        public PaymentStatus reconcileTransaction(Payment p)
        {
            if (p.providerRef == null) return p.status;

            GatewayResponse resp = PaymentGateway.queryStatus(p.providerRef);
            verifyCallback(resp, p);

            var qr = p as QRPayment;
            if (qr != null && p.status == PaymentStatus.Pending && qr.isExpired())
                p.updateStatus(PaymentStatus.TimedOut);

            return p.status;
        }

        public void simulateCustomerScan(Payment p)
        {
            PaymentGateway.simulateScan(p.providerRef);
        }

        private static bool Luhn(string digits)
        {
            int sum = 0;
            bool alt = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int n = digits[i] - '0';
                if (alt) { n *= 2; if (n > 9) n -= 9; }
                sum += n;
                alt = !alt;
            }
            return sum % 10 == 0;
        }

        private static bool ExpiryOk(string expiry)
        {
            DateTime d;
            if (string.IsNullOrWhiteSpace(expiry)) return false;
            if (!DateTime.TryParseExact(expiry.Trim(), "MM/yy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return false;
            return new DateTime(d.Year, d.Month, 1).AddMonths(1) > DateTime.Now;
        }
    }
}