using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace TVM
{
    public class TVMController
    {
        public const int SessionTimeoutSeconds = 60;

        public List<TVM.Transaction> Transaction
        { get; set; }

        public TVMScreen TVMScreen
        { get; set; }

        public PaymentService PaymentService
        { get; set; }

        public TransactionRepository TransactionRepository
        { get; set; }

        public TicketPrinter TicketPrinter
        { get; set; }

        public TVM.Transaction Current
        { get; private set; }

        public List<Destination> Destinations
        { get; private set; }

        public event Action<string> Logged;

        public TVMController(string connectionString)
        {
            Transaction = new List<TVM.Transaction>();
            TVMScreen = new TVMScreen();
            PaymentService = new PaymentService();
            TransactionRepository = new TransactionRepository(connectionString);
            TicketPrinter = new TicketPrinter();
        }

        public List<Destination> startTransaction()
        {
            var sw = Stopwatch.StartNew();
            Current = null;
            Destinations = TransactionRepository.getDestinations();
            sw.Stop();
            Check("NFR4 Start -> destination menu", sw.ElapsedMilliseconds, 2000);
            return Destinations;
        }

        public void selectDestination(Destination destination)
        {
            var sw = Stopwatch.StartNew();
            Fare fare = TransactionRepository.getFare(destination.destinationId);
            destination.Fare = fare;

            var t = new TVM.Transaction("TX" + DateTime.Now.ToString("yyyyMMddHHmmssfff"), fare.amount);
            t.Destination = destination;
            t.Payment = new List<Payment>();
            Current = t;
            Transaction.Add(t);
            TransactionRepository.saveTransaction(t);
            sw.Stop();
            Check("NFR5 Select destination", sw.ElapsedMilliseconds, 1000);
        }

        public Payment processPayment(string method, string cardNumber = null, string expiry = null, string cvv = null)
        {
            if (Current == null) throw new InvalidOperationException("No transaction in progress.");

            foreach (Payment old in Current.Payment.ToList())
            {
                if (old.status == PaymentStatus.TimedOut || old.status == PaymentStatus.Pending || old.status == PaymentStatus.Initiated)
                    PaymentService.reconcileTransaction(old);
                if (old.status == PaymentStatus.Verified)
                    throw new InvalidOperationException("This transaction is already paid.\nGiao dịch này đã được thanh toán.");
            }

            var sw = Stopwatch.StartNew();
            Payment p = PaymentService.createPayment(method, Current.amount, cardNumber, expiry, cvv);
            Current.Payment.Add(p);

            if (p.status == PaymentStatus.TimedOut)
            {
                PaymentService.reconcileTransaction(p);
                log("Payment timeout -> reconciled, status = " + p.status);
            }

            Current.updateStatus(p.status == PaymentStatus.Failed || p.status == PaymentStatus.TimedOut
                ? TransactionStatus.Failed : TransactionStatus.Pending);
            TransactionRepository.saveTransaction(Current);
            sw.Stop();
            log(method + " payment " + p.paymentId + " -> " + p.status + " (" + sw.ElapsedMilliseconds + " ms)");
            return p;
        }

        public PaymentStatus checkPayment(Payment p)
        {
            PaymentStatus st = PaymentService.reconcileTransaction(p);
            if ((st == PaymentStatus.TimedOut || st == PaymentStatus.Failed) && Current != null)
            {
                Current.updateStatus(TransactionStatus.Failed);
                TransactionRepository.saveTransaction(Current);
                log("QR payment " + p.paymentId + " -> " + st);
            }
            return st;
        }

        public Ticket paymentConfirmed()
        {
            Payment pay = Current.Payment.Last(x => x.status == PaymentStatus.Verified);
            Current.updateStatus(TransactionStatus.Paid);

            var sw = Stopwatch.StartNew();
            var ticket = new Ticket
            {
                ticketId = "TK" + DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                issuedAt = DateTime.Now,
                status = TicketStatus.Issued
            };
            ticket.generateBarcode();
            Current.Ticket = ticket;

            if (TicketPrinter.printTicket(ticket))
            {
                Current.updateStatus(TransactionStatus.Completed);
                log("Ticket " + ticket.ticketId + " printed");
            }
            else
            {
                TransactionRepository.logPrinterFailure(Current.transactionId, "Printer out of paper or unavailable");
                var refund = new Refund
                {
                    refundId = "RF" + DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                    amount = pay.amount,
                    reason = "Printer failure"
                };
                refund.request();
                refund.status = PaymentService.PaymentGateway.refund(pay.providerRef, pay.amount) ? "Completed" : "Failed";
                if (pay.Refund == null) pay.Refund = new List<Refund>();
                pay.Refund.Add(refund);

                ticket.status = TicketStatus.Refunded;
                Current.updateStatus(TransactionStatus.Failed);
                log("PRINTER FAILURE -> refund " + refund.refundId + " " + refund.status);
            }

            TransactionRepository.saveTransaction(Current);
            sw.Stop();
            Check("Ticket issue + save", sw.ElapsedMilliseconds, 3000);
            return ticket;
        }

        public void returnToIdle()
        {
            if (Current != null)
            {
                bool paid = Current.Payment != null && Current.Payment.Any(x => x.status == PaymentStatus.Verified);
                bool open = Current.status == TransactionStatus.Created
                         || Current.status == TransactionStatus.Pending
                         || Current.status == TransactionStatus.Failed;
                if (open && !paid)
                {
                    Current.cancel();
                    TransactionRepository.saveTransaction(Current);
                    log("Transaction " + Current.transactionId + " cancelled");
                }
            }
            Current = null;
        }

        private void Check(string what, long ms, long limitMs)
        {
            log("[" + (ms <= limitMs ? "PASS" : "FAIL") + "] " + what + ": " + ms + " ms (limit " + limitMs + " ms)");
        }

        private void log(string message)
        {
            var handler = Logged;
            if (handler != null) handler(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
        }
    }
}