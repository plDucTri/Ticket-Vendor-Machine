using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class TVMController
    {
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

        public void startTransaction()
        {
            throw new System.NotImplementedException();
        }

        public void selectDestination()
        {
            throw new System.NotImplementedException();
        }

        public void processPayment()
        {
            throw new System.NotImplementedException();
        }

        public void paymentConfirmed()
        {
            throw new System.NotImplementedException();
        }

        public void returnToIdle()
        {
            throw new System.NotImplementedException();
        }
    }
}