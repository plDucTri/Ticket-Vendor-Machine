using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class Transaction
    {
        public Transaction(string transactionId, decimal amount)
        {
            this.transactionId = transactionId;
            this.amount = amount;
            this.status = TransactionStatus.Created;
            this.startTime = DateTime.Now;
        }

        public string transactionId
        { get; set; }

        public decimal amount
        { get; set; }

        public TransactionStatus status { get; set; }

        public DateTime startTime
        { get; set; }

        public List<TVM.Payment> Payment
        { get; set; }

        public Destination Destination
        { get; set; }

        public Ticket Ticket
        { get; set; }

        public void updateStatus(TransactionStatus newStatus)
        {
            status = newStatus;
        }

        public void cancel()
        {
            status = TransactionStatus.Cancelled;
        }
    }
}