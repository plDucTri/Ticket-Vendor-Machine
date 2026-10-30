/* ============================================================
   TVM Demo database  (Phase 4)
   Re-runnable: drops and recreates everything.
   Run in SSMS or Visual Studio (SQL Server Object Explorer).
   Names mirror the C# classes in Phase4_TVM.
   ============================================================ */

IF DB_ID(N'TVMDemo') IS NULL
    CREATE DATABASE TVMDemo;
GO

USE TVMDemo;
GO

-- Drop in dependency order so the script can be re-run
IF OBJECT_ID(N'dbo.PrinterFailureLog', N'U') IS NOT NULL DROP TABLE dbo.PrinterFailureLog;
IF OBJECT_ID(N'dbo.Refund',            N'U') IS NOT NULL DROP TABLE dbo.Refund;
IF OBJECT_ID(N'dbo.Ticket',            N'U') IS NOT NULL DROP TABLE dbo.Ticket;
IF OBJECT_ID(N'dbo.Payment',           N'U') IS NOT NULL DROP TABLE dbo.Payment;
IF OBJECT_ID(N'dbo.TvmTransaction',    N'U') IS NOT NULL DROP TABLE dbo.TvmTransaction;
IF OBJECT_ID(N'dbo.Destination',       N'U') IS NOT NULL DROP TABLE dbo.Destination;
IF OBJECT_ID(N'dbo.Fare',              N'U') IS NOT NULL DROP TABLE dbo.Fare;
GO

-- Fare (class Fare: fareId, amount, effectiveDate)
CREATE TABLE dbo.Fare (
    fareId        NVARCHAR(20)  NOT NULL PRIMARY KEY,
    amount        DECIMAL(12,0) NOT NULL CHECK (amount > 0),   -- VND
    effectiveDate DATE          NOT NULL
);

-- Destination (class Destination: destinationId, name, Fare)
CREATE TABLE dbo.Destination (
    destinationId NVARCHAR(20)  NOT NULL PRIMARY KEY,
    name          NVARCHAR(100) NOT NULL,
    transport     NVARCHAR(20)  NOT NULL DEFAULT N'MRT',
    fareId        NVARCHAR(20)  NOT NULL REFERENCES dbo.Fare(fareId)
);

-- Transaction (class Transaction). "Transaction" is a reserved word, so the table is TvmTransaction.
CREATE TABLE dbo.TvmTransaction (
    transactionId NVARCHAR(30)  NOT NULL PRIMARY KEY,
    amount        DECIMAL(12,0) NOT NULL,
    status        NVARCHAR(20)  NOT NULL
        CHECK (status IN (N'Created', N'Pending', N'Paid', N'Failed', N'Completed', N'Cancelled')),
    startTime     DATETIME2(0)  NOT NULL,
    destinationId NVARCHAR(20)  NOT NULL REFERENCES dbo.Destination(destinationId)
);

-- Payment (abstract Payment + CardPayment + QRPayment in one table)
CREATE TABLE dbo.Payment (
    paymentId         NVARCHAR(30)  NOT NULL PRIMARY KEY,
    transactionId     NVARCHAR(30)  NOT NULL REFERENCES dbo.TvmTransaction(transactionId),
    paymentType       NVARCHAR(10)  NOT NULL CHECK (paymentType IN (N'Card', N'QR')),
    amount            DECIMAL(12,0) NOT NULL,
    status            NVARCHAR(20)  NOT NULL
        CHECK (status IN (N'Initiated', N'Pending', N'Verified', N'Failed', N'TimedOut')),
    providerRef       NVARCHAR(60)  NULL,
    maskedCardNumber  NVARCHAR(25)  NULL,   -- CardPayment only, e.g. **** **** **** 4242
    authorizationCode NVARCHAR(20)  NULL,   -- CardPayment only
    qrPayload         NVARCHAR(400) NULL,   -- QRPayment only
    expiresAt         DATETIME2(0)  NULL    -- QRPayment only
);

-- Ticket (class Ticket: ticketId, barcode, issuedAt, status)
CREATE TABLE dbo.Ticket (
    ticketId      NVARCHAR(30) NOT NULL PRIMARY KEY,
    transactionId NVARCHAR(30) NOT NULL UNIQUE REFERENCES dbo.TvmTransaction(transactionId),
    barcode       NVARCHAR(40) NOT NULL,
    issuedAt      DATETIME2(0) NOT NULL,
    status        NVARCHAR(20) NOT NULL
        CHECK (status IN (N'Issued', N'Printed', N'PrintFailed', N'Refunded'))
);

-- Refund (class Refund: refundId, amount, reason, status)
CREATE TABLE dbo.Refund (
    refundId  NVARCHAR(30)  NOT NULL PRIMARY KEY,
    paymentId NVARCHAR(30)  NOT NULL REFERENCES dbo.Payment(paymentId),
    amount    DECIMAL(12,0) NOT NULL,
    reason    NVARCHAR(200) NULL,
    status    NVARCHAR(20)  NOT NULL
);

-- Printer failure log (TransactionRepository.logPrinterFailure)
CREATE TABLE dbo.PrinterFailureLog (
    logId         INT IDENTITY(1,1) PRIMARY KEY,
    transactionId NVARCHAR(30)  NULL REFERENCES dbo.TvmTransaction(transactionId),
    reason        NVARCHAR(200) NOT NULL,
    loggedAt      DATETIME2(0)  NOT NULL DEFAULT SYSDATETIME()
);
GO

-- Sample data (fares are demo values in VND, not official fares)
INSERT INTO dbo.Fare (fareId, amount, effectiveDate) VALUES
    (N'F01',  8000, '2026-01-01'),
    (N'F02', 10000, '2026-01-01'),
    (N'F03', 12000, '2026-01-01'),
    (N'F04', 15000, '2026-01-01'),
    (N'F05', 20000, '2026-01-01');

INSERT INTO dbo.Destination (destinationId, name, transport, fareId) VALUES
    (N'D01', N'Nhà hát Thành phố',  N'MRT', N'F01'),
    (N'D02', N'Ba Son',             N'MRT', N'F01'),
    (N'D03', N'Thảo Điền',          N'MRT', N'F02'),
    (N'D04', N'Phước Long',         N'MRT', N'F03'),
    (N'D05', N'Thủ Đức',            N'MRT', N'F04'),
    (N'D06', N'Suối Tiên',          N'MRT', N'F05'),
    (N'D07', N'Bến xe Miền Đông',   N'Bus', N'F02'),
    (N'D08', N'Sân bay Tân Sơn Nhất', N'Bus', N'F03');
GO

-- Quick check: should list 8 destinations with fares
SELECT d.destinationId, d.name, d.transport, f.amount AS fareVND
FROM dbo.Destination d JOIN dbo.Fare f ON f.fareId = d.fareId
ORDER BY d.destinationId;
GO
