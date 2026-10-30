using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using TVM;

namespace TVMDemo
{
    public partial class Form1 : Form
    {
        private TVMController ctl;
        private string connStr;

        private FlowLayoutPanel pnlStart, pnlDest, pnlMethod, pnlCard, pnlQr, pnlResult, pnlHistory;
        private FlowLayoutPanel current;
        private ListBox lstDest, lstLog;
        private Label lblDb, lblFare, lblMethodInfo, lblCardMsg, lblQrInfo, lblQrTimer, lblResultTitle, lblResultBody;
        private TextBox txtCard, txtExp, txtCvv;
        private PictureBox picQr, picBarcode;
        private CheckBox chkPaper;
        private DataGridView grid;
        private Button btnStart;
        private System.Windows.Forms.Timer timer;

        private DateTime lastActivity = DateTime.Now;
        private QRPayment qrPayment;
        private bool busy;

        public Form1()
        {
            InitializeComponent();
            BuildUi();
            Load -= Form1_Load;
            Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                connStr = ConfigurationManager.ConnectionStrings["TVMDb"].ConnectionString;
                using (var c = new SqlConnection(connStr)) { c.Open(); }

                ctl = new TVMController(connStr);
                ctl.Logged += s =>
                {
                    lstLog.Items.Add(s);
                    lstLog.TopIndex = lstLog.Items.Count - 1;
                };
                lblDb.Text = "Database: connected";
                lblDb.ForeColor = Color.DarkGreen;
                btnStart.Enabled = true;
            }
            catch (Exception ex)
            {
                lblDb.Text = "Database: NOT connected";
                lblDb.ForeColor = Color.Firebrick;
                MessageBox.Show("Service unavailable: cannot reach the database.\nDịch vụ không khả dụng: không kết nối được cơ sở dữ liệu.\n\n" + ex.Message,
                    "TVM", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            ShowScreen(pnlStart);
            timer.Start();
        }

        private void BuildUi()
        {
            Text = "Ticket Vendor Machine (TVM) - Demo";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1060, 680);
            Font = new Font("Segoe UI", 11f);
            BackColor = Color.White;

            lstLog = new ListBox { Dock = DockStyle.Right, Width = 340, Font = new Font("Consolas", 8.5f), HorizontalScrollbar = true };
            Controls.Add(lstLog);

            timer = new System.Windows.Forms.Timer { Interval = 1000 };
            timer.Tick += Timer_Tick;

            pnlStart = NewScreen("Ticket Vendor Machine", "Máy bán vé tự động");
            lblDb = new Label { AutoSize = true, Text = "Database: ...", Margin = new Padding(8, 20, 8, 4) };
            btnStart = MakeBtn("START  /  BẮT ĐẦU", (s, e) => Safe(OnStart));
            btnStart.Enabled = false;
            chkPaper = new CheckBox { AutoSize = true, Text = "Demo: simulate printer out of paper", Margin = new Padding(8, 12, 8, 4) };
            chkPaper.CheckedChanged += (s, e) => { if (ctl != null) ctl.TicketPrinter.paperLevel = chkPaper.Checked ? 5 : 100; };
            var btnReport = MakeBtn("Transaction report / Báo cáo", (s, e) => Safe(OnReport));
            pnlStart.Controls.AddRange(new Control[] { btnStart, btnReport, chkPaper, lblDb });

            pnlDest = NewScreen("Select destination", "Chọn điểm đến");
            lstDest = new ListBox { Width = 620, Height = 300, Font = new Font("Consolas", 12f), Margin = new Padding(8) };
            lstDest.SelectedIndexChanged += (s, e) =>
            {
                Touch();
                var d = SelectedDestination();
                lblFare.Text = d == null ? "Fare / Giá vé: -" : "Fare / Giá vé: " + ctl.TVMScreen.displayFare(d.Fare);
            };
            lblFare = new Label { AutoSize = true, Text = "Fare / Giá vé: -", Font = new Font("Segoe UI", 14f, FontStyle.Bold), Margin = new Padding(8) };
            pnlDest.Controls.AddRange(new Control[]
            {
                lstDest, lblFare,
                MakeBtn("Confirm / Xác nhận", (s, e) => Safe(OnDestinationConfirm)),
                MakeBtn("Cancel / Hủy", (s, e) => Safe(GoIdle), true)
            });

            pnlMethod = NewScreen("Choose payment method", "Chọn phương thức thanh toán");
            lblMethodInfo = new Label { AutoSize = true, Margin = new Padding(8), Font = new Font("Segoe UI", 13f) };
            pnlMethod.Controls.AddRange(new Control[]
            {
                lblMethodInfo,
                MakeBtn("Credit / debit card  -  Thẻ", (s, e) => { Touch(); ClearCardForm(); ShowScreen(pnlCard); }),
                MakeBtn("QR code  -  Mã QR", (s, e) => Safe(OnQr)),
                MakeBtn("Cancel / Hủy", (s, e) => Safe(GoIdle), true)
            });

            pnlCard = NewScreen("Card payment", "Thanh toán bằng thẻ");
            txtCard = new TextBox { Width = 320, MaxLength = 23, Margin = new Padding(8, 2, 8, 8) };
            txtExp = new TextBox { Width = 120, MaxLength = 5, Margin = new Padding(8, 2, 8, 8) };
            txtCvv = new TextBox { Width = 120, MaxLength = 4, UseSystemPasswordChar = true, Margin = new Padding(8, 2, 8, 8) };
            txtCard.TextChanged += (s, e) => Touch();
            txtExp.TextChanged += (s, e) => Touch();
            txtCvv.TextChanged += (s, e) => Touch();
            lblCardMsg = new Label { AutoSize = true, ForeColor = Color.Firebrick, Margin = new Padding(8) };
            var hint = new Label
            {
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(8),
                Text = "Demo cards:  4242 4242 4242 4242 = approved\n" +
                       "                   4000 0000 0000 0002 = declined\n" +
                       "                   4000 0000 0000 0010 = bank does not answer (timeout)"
            };
            pnlCard.Controls.AddRange(new Control[]
            {
                new Label { AutoSize = true, Text = "Card number / Số thẻ", Margin = new Padding(8, 8, 8, 0) }, txtCard,
                new Label { AutoSize = true, Text = "Expiry MM/yy / Hết hạn", Margin = new Padding(8, 0, 8, 0) }, txtExp,
                new Label { AutoSize = true, Text = "CVV", Margin = new Padding(8, 0, 8, 0) }, txtCvv,
                lblCardMsg, hint,
                MakeBtn("Pay / Thanh toán", (s, e) => Safe(OnPayCard)),
                MakeBtn("Back / Quay lại", (s, e) => { Touch(); ShowScreen(pnlMethod); }, true)
            });

            pnlQr = NewScreen("Scan to pay", "Quét mã để thanh toán");
            picQr = new PictureBox { Width = 250, Height = 250, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(8) };
            lblQrInfo = new Label { AutoSize = true, Margin = new Padding(8), ForeColor = Color.Gray, Font = new Font("Segoe UI", 9f) };
            lblQrTimer = new Label { AutoSize = true, Margin = new Padding(8), Font = new Font("Segoe UI", 13f, FontStyle.Bold) };
            pnlQr.Controls.AddRange(new Control[]
            {
                picQr, lblQrInfo, lblQrTimer,
                MakeBtn("DEMO: customer paid / đã thanh toán", (s, e) => Safe(() => { ctl.PaymentService.simulateCustomerScan(qrPayment); PollQr(); })),
                MakeBtn("DEMO: no answer (timeout)", (s, e) => Safe(() => { qrPayment.expiresAt = DateTime.Now.AddSeconds(-1); PollQr(); }), true),
                MakeBtn("Cancel / Hủy", (s, e) => Safe(() => { qrPayment = null; GoIdle(); }), true)
            });

            pnlResult = NewScreen("", "");
            lblResultTitle = (Label)pnlResult.Controls[0];
            picBarcode = new PictureBox { Width = 440, Height = 90, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(8) };
            lblResultBody = new Label { AutoSize = true, Margin = new Padding(8), Font = new Font("Consolas", 11f) };
            pnlResult.Controls.AddRange(new Control[]
            {
                picBarcode, lblResultBody,
                MakeBtn("Done / Hoàn tất", (s, e) => Safe(GoIdle))
            });

            pnlHistory = NewScreen("Transaction report", "Báo cáo giao dịch");
            grid = new DataGridView
            {
                Width = 680,
                Height = 480,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                Margin = new Padding(8)
            };
            pnlHistory.Controls.AddRange(new Control[] { grid, MakeBtn("Back / Quay lại", (s, e) => ShowScreen(pnlStart), true) });
        }

        private FlowLayoutPanel NewScreen(string en, string vi)
        {
            var p = new FlowLayoutPanel
            {
                Location = new Point(0, 0),
                Size = new Size(ClientSize.Width - 340, ClientSize.Height),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(24),
                Visible = false
            };
            p.Controls.Add(new Label { AutoSize = true, Text = en, Font = new Font("Segoe UI", 20f, FontStyle.Bold), Margin = new Padding(8, 8, 8, 0) });
            if (vi != "")
                p.Controls.Add(new Label { AutoSize = true, Text = vi, ForeColor = Color.Gray, Margin = new Padding(8, 0, 8, 12) });
            Controls.Add(p);
            return p;
        }

        private Button MakeBtn(string text, EventHandler onClick, bool secondary = false)
        {
            var b = new Button
            {
                Text = text,
                Size = new Size(380, 46),
                Margin = new Padding(8),
                FlatStyle = FlatStyle.Flat,
                BackColor = secondary ? Color.WhiteSmoke : Color.SteelBlue,
                ForeColor = secondary ? Color.Black : Color.White
            };
            b.Click += onClick;
            return b;
        }

        private void ShowScreen(FlowLayoutPanel p)
        {
            foreach (var panel in new[] { pnlStart, pnlDest, pnlMethod, pnlCard, pnlQr, pnlResult, pnlHistory })
                panel.Visible = panel == p;
            current = p;
            Touch();
        }

        private void Touch() { lastActivity = DateTime.Now; }

        private void OnStart()
        {
            chkPaper_Apply();
            List<Destination> list = ctl.startTransaction();
            lstDest.Items.Clear();
            foreach (string line in ctl.TVMScreen.displayDestinations(list)) lstDest.Items.Add(line);
            lblFare.Text = "Fare / Giá vé: -";
            ShowScreen(pnlDest);
        }

        private void chkPaper_Apply()
        {
            ctl.TicketPrinter.paperLevel = chkPaper.Checked ? 5 : Math.Max(ctl.TicketPrinter.paperLevel, 10);
        }

        private Destination SelectedDestination()
        {
            int i = lstDest.SelectedIndex;
            return (ctl == null || ctl.Destinations == null || i < 0) ? null : ctl.Destinations[i];
        }

        private void OnDestinationConfirm()
        {
            Destination d = SelectedDestination();
            if (d == null)
            {
                Msg("Please select a destination.", "Vui lòng chọn điểm đến.");
                return;
            }
            ctl.selectDestination(d);
            lblMethodInfo.Text = "Destination / Điểm đến: " + d.name + "\nFare / Giá vé: " + ctl.TVMScreen.displayFare(d.Fare);
            ShowScreen(pnlMethod);
        }

        private void ClearCardForm()
        {
            txtCard.Text = ""; txtExp.Text = ""; txtCvv.Text = ""; lblCardMsg.Text = "";
        }

        private void OnPayCard()
        {
            lblCardMsg.Text = "";
            Payment p = ctl.processPayment("Card", txtCard.Text, txtExp.Text, txtCvv.Text);

            if (p.status == PaymentStatus.Verified)
            {
                ShowResult();
            }
            else if (p.status == PaymentStatus.Failed)
            {
                lblCardMsg.Text = "Card declined - no charge, no ticket. Try another card.\nThẻ bị từ chối - không bị trừ tiền, chưa phát hành vé.";
                Msg("Card declined. No ticket was issued.", "Thẻ bị từ chối. Vé chưa được phát hành.");
            }
            else
            {
                Msg("The bank did not answer within the time limit. The payment status was checked again: you were NOT charged and no ticket was issued. Please try again or use another method.",
                    "Ngân hàng không phản hồi kịp thời. Trạng thái đã được đối soát: bạn CHƯA bị trừ tiền và chưa có vé. Vui lòng thử lại hoặc chọn phương thức khác.");
                ShowScreen(pnlMethod);
            }
        }

        private void OnQr()
        {
            Payment p = ctl.processPayment("QR");
            qrPayment = p as QRPayment;
            if (qrPayment == null || qrPayment.status != PaymentStatus.Pending)
            {
                Msg("Could not create the QR payment. Please try again.", "Không tạo được thanh toán QR. Vui lòng thử lại.");
                return;
            }
            string payload = ctl.TVMScreen.displayQRCode(qrPayment);
            picQr.Image = DrawQr(payload);
            lblQrInfo.Text = payload + "\n(demo QR image - not scannable)";
            lblQrTimer.Text = "Expires in / Hết hạn sau: " + PaymentService.PaymentTimeoutSeconds + " s";
            ShowScreen(pnlQr);
        }

        private void PollQr()
        {
            if (qrPayment == null) return;
            PaymentStatus st = ctl.checkPayment(qrPayment);
            int left = (int)Math.Max(0, (qrPayment.expiresAt - DateTime.Now).TotalSeconds);
            lblQrTimer.Text = "Expires in / Hết hạn sau: " + left + " s";

            if (st == PaymentStatus.Verified)
            {
                qrPayment = null;
                ShowResult();
            }
            else if (st == PaymentStatus.TimedOut || st == PaymentStatus.Failed)
            {
                qrPayment = null;
                Msg("No payment received before the QR code expired. You were not charged and no ticket was issued.",
                    "Chưa nhận được thanh toán trước khi mã QR hết hạn. Bạn chưa bị trừ tiền và chưa có vé.");
                ShowScreen(pnlMethod);
            }
        }

        private void ShowResult()
        {
            Ticket tk = ctl.paymentConfirmed();
            TVM.Transaction t = ctl.Current;
            Payment pay = t.Payment.Last(x => x.status == PaymentStatus.Verified);
            string method = (pay is QRPayment) ? "QR code" : "Card " + ((CardPayment)pay).maskedCardNumber;

            var sb = new StringBuilder();
            sb.AppendLine("Ticket ID    : " + tk.ticketId);
            sb.AppendLine("Barcode      : " + tk.barcode);
            sb.AppendLine("Destination  : " + t.Destination.name);
            sb.AppendLine("Fare         : " + ctl.TVMScreen.displayFare(t.Destination.Fare));
            sb.AppendLine("Payment      : " + method);
            sb.AppendLine("Issued at    : " + tk.issuedAt.ToString("dd/MM/yyyy HH:mm:ss"));
            sb.AppendLine("Transaction  : " + t.transactionId);

            if (tk.status == TicketStatus.Printed)
            {
                lblResultTitle.Text = "Payment successful - ticket printed";
                lblResultTitle.ForeColor = Color.DarkGreen;
                picBarcode.Image = DrawBarcode(tk.barcode);
                picBarcode.Visible = true;
            }
            else
            {
                lblResultTitle.Text = "Printer failure - payment refunded";
                lblResultTitle.ForeColor = Color.Firebrick;
                picBarcode.Visible = false;
                Refund rf = pay.Refund.Last();
                sb.AppendLine("Refund       : " + rf.refundId + " (" + rf.status + ")");
                sb.AppendLine();
                sb.AppendLine("The ticket could not be printed. Your payment is being refunded.");
                sb.AppendLine("Không in được vé. Tiền của bạn sẽ được hoàn lại.");
            }
            lblResultBody.Text = sb.ToString();
            ShowScreen(pnlResult);
        }

        private void OnReport()
        {
            grid.DataSource = ctl.TransactionRepository.getHistory();
            ShowScreen(pnlHistory);
        }

        private void GoIdle()
        {
            ctl.returnToIdle();
            ShowScreen(pnlStart);
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (ctl == null || busy) return;

            if (current == pnlDest || current == pnlMethod || current == pnlCard)
            {
                if ((DateTime.Now - lastActivity).TotalSeconds > TVMController.SessionTimeoutSeconds)
                {
                    Safe(() =>
                    {
                        GoIdle();
                        Msg("Session expired after 60 seconds of inactivity. Please start again.",
                            "Phiên giao dịch đã hết hạn sau 60 giây không hoạt động. Vui lòng bắt đầu lại.");
                    });
                }
            }
            else if (current == pnlQr && qrPayment != null)
            {
                Safe(PollQr);
            }
        }

        private void Msg(string en, string vi)
        {
            MessageBox.Show(ctl.TVMScreen.displayMessage(en, vi), "TVM", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Safe(Action action)
        {
            if (busy) return;
            busy = true;
            try
            {
                action();
            }
            catch (ArgumentException ex)
            {
                lblCardMsg.Text = ex.Message.Replace("\n", " ");
                MessageBox.Show(ex.Message, "Invalid input / Dữ liệu không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (SqlException)
            {
                MessageBox.Show("Service unavailable (database). No ticket was issued and you were not charged.\n\nDịch vụ không khả dụng (cơ sở dữ liệu). Vé chưa được phát hành.",
                    "TVM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { ctl.returnToIdle(); } catch { }
                ShowScreen(pnlStart);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Something went wrong. Please try again.\nĐã xảy ra lỗi. Vui lòng thử lại.\n\n" + ex.Message,
                    "TVM", MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { ctl.returnToIdle(); } catch { }
                ShowScreen(pnlStart);
            }
            finally
            {
                busy = false;
            }
        }

        private static Bitmap DrawQr(string payload)
        {
            const int N = 25, cell = 10;
            var m = new bool[N, N];
            byte[] h;
            using (var sha = SHA256.Create()) h = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var rnd = new Random(BitConverter.ToInt32(h, 0));
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    m[y, x] = rnd.Next(2) == 1;

            foreach (var o in new[] { new Point(0, 0), new Point(N - 7, 0), new Point(0, N - 7) })
                for (int dy = 0; dy < 7; dy++)
                    for (int dx = 0; dx < 7; dx++)
                        m[o.Y + dy, o.X + dx] = dy == 0 || dy == 6 || dx == 0 || dx == 6 || (dy >= 2 && dy <= 4 && dx >= 2 && dx <= 4);

            var bmp = new Bitmap(N * cell, N * cell);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                        if (m[y, x]) g.FillRectangle(Brushes.Black, x * cell, y * cell, cell, cell);
            }
            return bmp;
        }

        private static Bitmap DrawBarcode(string text)
        {
            var bmp = new Bitmap(440, 90);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                int x = 10;
                foreach (char ch in text)
                {
                    for (int bit = 7; bit >= 0 && x < 430; bit--)
                    {
                        int w = ((ch >> bit) & 1) == 1 ? 3 : 1;
                        g.FillRectangle(Brushes.Black, x, 8, w, 74);
                        x += w + 2;
                    }
                }
            }
            return bmp;
        }
    }
}