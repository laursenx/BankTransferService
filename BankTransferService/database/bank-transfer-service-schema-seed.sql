-- Bank Transfer Service - schema + seed data
-- SQL Server / Azure SQL style script
-- Program code and database object names are intentionally in English.

IF OBJECT_ID('dbo.Transfers', 'U') IS NOT NULL DROP TABLE dbo.Transfers;
IF OBJECT_ID('dbo.Accounts', 'U') IS NOT NULL DROP TABLE dbo.Accounts;
GO

CREATE TABLE dbo.Accounts
(
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    AccountNumber   VARCHAR(20)      NOT NULL UNIQUE,
    OwnerName       NVARCHAR(100)    NOT NULL,
    Balance         DECIMAL(18,2)    NOT NULL,
    OverdraftLimit  DECIMAL(18,2)    NOT NULL CONSTRAINT DF_Accounts_OverdraftLimit DEFAULT (0),
    IsActive        BIT              NOT NULL CONSTRAINT DF_Accounts_IsActive DEFAULT (1)
);
GO

CREATE TABLE dbo.Transfers
(
    Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    FromAccountId UNIQUEIDENTIFIER NOT NULL,
    ToAccountId   UNIQUEIDENTIFIER NOT NULL,
    Amount        DECIMAL(18,2)    NOT NULL,
    Reference     NVARCHAR(140)    NOT NULL,
    Description   NVARCHAR(300)    NULL,
    CreatedUtc    DATETIME2        NOT NULL CONSTRAINT DF_Transfers_CreatedUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Transfers_FromAccount      FOREIGN KEY (FromAccountId) REFERENCES dbo.Accounts(Id),
    CONSTRAINT FK_Transfers_ToAccount        FOREIGN KEY (ToAccountId)   REFERENCES dbo.Accounts(Id),
    CONSTRAINT CK_Transfers_PositiveAmount   CHECK (Amount > 0),
    CONSTRAINT CK_Transfers_DifferentAccounts CHECK (FromAccountId <> ToAccountId)
);
GO

CREATE INDEX IX_Transfers_FromAccountId ON dbo.Transfers(FromAccountId, CreatedUtc DESC);
CREATE INDEX IX_Transfers_ToAccountId   ON dbo.Transfers(ToAccountId,   CreatedUtc DESC);
GO

-- ============================================================
-- Seed data
-- Fixed GUIDs allow repeatable test references.
-- ============================================================

INSERT INTO dbo.Accounts (Id, AccountNumber, OwnerName, Balance, OverdraftLimit, IsActive)
VALUES
    ('11111111-1111-1111-1111-111111111111', '1001', 'Operating North',  5000.00,   0.00, 1),
    ('22222222-2222-2222-2222-222222222222', '1002', 'Operating South',  1250.00,   0.00, 1),
    ('33333333-3333-3333-3333-333333333333', '2001', 'Private Buffer',    150.00, 200.00, 1),
    ('44444444-4444-4444-4444-444444444444', '3001', 'Savings Vault',   10000.00,   0.00, 1),
    ('99999999-9999-9999-9999-999999999999', '9001', 'Dormant Account',   800.00,   0.00, 0);
GO

INSERT INTO dbo.Transfers (Id, FromAccountId, ToAccountId, Amount, Reference, Description, CreatedUtc)
VALUES
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1', '44444444-4444-4444-4444-444444444444', '11111111-1111-1111-1111-111111111111', 500.00,  'Initial funding', 'Example seed transfer',    '2026-02-01T08:00:00'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2', '11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222', 125.50,  'Invoice 1006',    'Example payment',          '2026-02-07T10:15:00'),
    ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3', '33333333-3333-3333-3333-333333333333', '11111111-1111-1111-1111-111111111111',  75.00,  'Pocket transfer', 'Example internal move',    '2026-02-10T12:45:00');
GO

-- Helpful queries for quick manual checks

SELECT * FROM dbo.Accounts  ORDER BY AccountNumber;
SELECT * FROM dbo.Transfers ORDER BY CreatedUtc DESC;
GO
