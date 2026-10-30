using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace TVM
{
    public class TransactionRepository
    {
        private readonly string connectionString;

        public Transaction Transaction
        { get; set; }

        public TransactionRepository(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public List<Destination> getDestinations()
        {
            var list = new List<Destination>();
            const string sql =
                @"SELECT d.destinationId, d.name, f.fareId, f.amount, f.effectiveDate
                  FROM Destination d JOIN Fare f ON f.fareId = d.fareId
                  ORDER BY d.destinationId";
            using (var c = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, c))
            {
                c.Open();
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(new Destination
                        {
                            destinationId = r.GetString(0),
                            name = r.GetString(1),
                            Fare = new Fare
                            {
                                fareId = r.GetString(2),
                                amount = r.GetDecimal(3),
                                effectiveDate = r.GetDateTime(4)
                            }
                        });
                    }
                }
            }
            return list;
        }

        public Fare getFare(string destinationId)
        {
            const string sql =
                @"SELECT TOP 1 f.fareId, f.amount, f.effectiveDate
                  FROM Destination d JOIN Fare f ON f.fareId = d.fareId
                  WHERE d.destinationId = @id AND f.effectiveDate <= CAST(GETDATE() AS DATE)";
            using (var c = new SqlConnection(connectionString))
            using (var cmd = new SqlCommand(sql, c))
            {
                cmd.Parameters.AddWithValue("@id", destinationId);
                c.Open();
                using (var r = cmd.ExecuteReader())
                {
                    if (!r.Read())
                        throw new InvalidOperationException("Fare not available for destination " + destinationId);
                    return new Fare { fareId = r.GetString(0), amount = r.GetDecimal(1), effectiveDate = r.GetDateTime(2) };
                }
            }
        }

        public void saveTransaction(Transaction t)
        {
            using (var c = new SqlConnection(connectionString))
            {
                c.Open();
                using (var tx = c.BeginTransaction())
                {
                    try
                    {
                        Exec(c, tx,
                            @"IF EXISTS (SELECT 1 FROM TvmTransaction WHERE transactionId = @id)
                                UPDATE TvmTransaction SET amount = @amt, status = @st WHERE transactionId = @id
                              ELSE
                                INSERT INTO TvmTransaction (transactionId, amount, status, startTime, destinationId)
                                VALUES (@id, @amt, @st, @start, @dest)",
                            P("@id", t.transactionId), P("@amt", t.amount), P("@st", t.status.ToString()),
                            P("@start", t.startTime), P("@dest", t.Destination.destinationId));

                        if (t.Payment != null)
                        {
                            foreach (Payment p in t.Payment)
                            {
                                var card = p as CardPayment;
                                var qr = p as QRPayment;
                                Exec(c, tx,
                                    @"IF EXISTS (SELECT 1 FROM Payment WHERE paymentId = @pid)
                                        UPDATE Payment SET status = @st, providerRef = @ref, authorizationCode = @auth, qrPayload = @qr
                                        WHERE paymentId = @pid
                                      ELSE
                                        INSERT INTO Payment (paymentId, transactionId, paymentType, amount, status, providerRef,
                                                             maskedCardNumber, authorizationCode, qrPayload, expiresAt)
                                        VALUES (@pid, @tid, @type, @amt, @st, @ref, @mask, @auth, @qr, @exp)",
                                    P("@pid", p.paymentId), P("@tid", t.transactionId),
                                    P("@type", qr != null ? "QR" : "Card"), P("@amt", p.amount),
                                    P("@st", p.status.ToString()), P("@ref", p.providerRef),
                                    P("@mask", card != null ? card.maskedCardNumber : null),
                                    P("@auth", card != null ? card.authorizationCode : null),
                                    P("@qr", qr != null ? qr.qrPayload : null),
                                    P("@exp", qr != null ? (object)qr.expiresAt : null));

                                if (p.Refund != null)
                                {
                                    foreach (Refund rf in p.Refund)
                                    {
                                        Exec(c, tx,
                                            @"IF EXISTS (SELECT 1 FROM Refund WHERE refundId = @rid)
                                                UPDATE Refund SET status = @st WHERE refundId = @rid
                                              ELSE
                                                INSERT INTO Refund (refundId, paymentId, amount, reason, status)
                                                VALUES (@rid, @pid, @amt, @reason, @st)",
                                            P("@rid", rf.refundId), P("@pid", p.paymentId), P("@amt", rf.amount),
                                            P("@reason", rf.reason), P("@st", rf.status));
                                    }
                                }
                            }
                        }

                        if (t.Ticket != null)
                        {
                            Exec(c, tx,
                                @"IF EXISTS (SELECT 1 FROM Ticket WHERE ticketId = @tkid)
                                    UPDATE Ticket SET status = @st WHERE ticketId = @tkid
                                  ELSE
                                    INSERT INTO Ticket (ticketId, transactionId, barcode, issuedAt, status)
                                    VALUES (@tkid, @tid, @bc, @at, @st)",
                                P("@tkid", t.Ticket.ticketId), P("@tid", t.transactionId), P("@bc", t.Ticket.barcode),
                                P("@at", t.Ticket.issuedAt), P("@st", t.Ticket.status.ToString()));
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public void logPrinterFailure(string transactionId, string reason)
        {
            using (var c = new SqlConnection(connectionString))
            {
                c.Open();
                Exec(c, null,
                    "INSERT INTO PrinterFailureLog (transactionId, reason) VALUES (@tid, @reason)",
                    P("@tid", transactionId), P("@reason", reason));
            }
        }

        public DataTable getHistory()
        {
            const string sql =
                @"SELECT TOP 100 t.transactionId AS Transaction_ID, d.name AS Destination, t.amount AS Fare_VND,
                         t.status AS Transaction_Status, p.paymentType AS Method, p.status AS Payment_Status,
                         tk.ticketId AS Ticket_ID, tk.status AS Ticket_Status, t.startTime AS Time
                  FROM TvmTransaction t
                  JOIN Destination d ON d.destinationId = t.destinationId
                  LEFT JOIN Payment p ON p.transactionId = t.transactionId
                  LEFT JOIN Ticket tk ON tk.transactionId = t.transactionId
                  ORDER BY t.startTime DESC, p.paymentId DESC";
            var table = new DataTable();
            using (var c = new SqlConnection(connectionString))
            using (var da = new SqlDataAdapter(sql, c))
            {
                da.Fill(table);
            }
            return table;
        }

        private static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        private static void Exec(SqlConnection c, SqlTransaction tx, string sql, params SqlParameter[] ps)
        {
            using (var cmd = new SqlCommand(sql, c, tx))
            {
                cmd.Parameters.AddRange(ps);
                cmd.ExecuteNonQuery();
            }
        }
    }
}