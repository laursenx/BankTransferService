# BankTransferService

A small ASP.NET Core Web API that handles money transfers between bank accounts. Built as a portfolio assignment for my S4.1 course — the focus is on clean backend architecture, raw SQL (no ORM), and proper transaction handling.

## What it does

The API lets you transfer money between accounts, look up account info, and view transfer history. Each transfer runs inside a single database transaction with row-level locks on both accounts, so balances can't drift if two requests hit the same source account at the same time.

### Endpoints

| Method | Route | What it does |
|--------|-------|-------------|
| `POST` | `/api/transfers` | Transfer money between two accounts |
| `GET` | `/api/accounts/{id}` | Get info about an account |
| `GET` | `/api/accounts/{id}/transfers` | Get transfer history for an account |
| `GET` | `/health` | Health check (verifies database connectivity) |

### Example: creating a transfer

```http
POST /api/transfers
Content-Type: application/json
Idempotency-Key: 6c1a3e4f-9b27-4d6f-9bda-91a1f6f3a013

{
  "fromAccountId": "11111111-1111-1111-1111-111111111111",
  "toAccountId":   "22222222-2222-2222-2222-222222222222",
  "amount": 100.00,
  "reference": "Invoice 2026-1007",
  "description": "Transfer for test run"
}
```

The `Idempotency-Key` header is optional. When provided, retrying the same request with the same key returns the original transfer instead of creating a second one — useful when the network drops between you and the API and you don't know whether the first call landed.

## Project structure

```
BankTransferService/
├── Controllers/                       → Thin HTTP layer
├── Services/
│   └── TransferService.cs             → Business rules + transaction orchestration
├── Data/
│   ├── SqlConnectionFactory.cs        → Provider-agnostic DbConnection factory
│   ├── AccountRepository.cs           → Account reads/writes (locking + non-locking)
│   ├── TransferRepository.cs          → Transfer inserts + idempotency lookup
│   └── TransferQueryRepository.cs     → Transfer history reads
├── Interfaces/                        → Contracts for DI
├── Models/
│   ├── Domain/                        → Account, Transfer, TransferResult, TransferStatus
│   ├── Requests/                      → TransferRequest (input DTO)
│   └── Responses/                     → TransferResponse, AccountResponse, ErrorResponse, TransferCreatedResponse
└── database/                          → SQL schema + seed data

BankTransferService.Tests/             → Unit tests (no database)
└── TransferServiceTests.cs

BankTransferService.IntegrationTests/  → Integration tests (real SQL Server via Testcontainers)
├── DatabaseFixture.cs
├── TestConnectionFactory.cs
└── TransferServiceIntegrationTests.cs
```

The layering is strict: controllers only handle HTTP, the service owns the business rules and the database transaction, and repositories only run SQL. Pure validation rules live in `TransferService.ValidateBusinessRules`, which means they can be unit-tested directly with constructed `Account` objects — no mocks, no I/O.

## Transfer rules

Before a transfer goes through, the service checks:

- Amount has to be greater than 0
- Can't transfer to the same account
- Both accounts need to exist and be active
- Sender can't go below their overdraft limit

If any of those fail, nothing gets saved. The debit, credit, and transfer log all happen in one transaction with `RepeatableRead` isolation and `UPDLOCK, HOLDLOCK` on both account rows.

## Getting started

**You'll need:**
- .NET 8 SDK
- SQL Server (LocalDB works fine for local development; the integration tests use Testcontainers and just need Docker)

### Database

First create a database called `BankTransferDb`, then run the seed script against it:

```bash
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -Q "CREATE DATABASE BankTransferDb"
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -d BankTransferDb -i BankTransferService/database/bank-transfer-service-schema-seed.sql
```

Or just open `database/bank-transfer-service-schema-seed.sql` in SSMS and run it there. It creates the tables and inserts some test accounts.

### Configuration

Copy the example settings and update the connection string:

```bash
cp BankTransferService/appsettings.Example.json BankTransferService/appsettings.json
```

In `appsettings.json`, set your connection string:

```json
"ConnectionStrings": {
  "BankDb": "Server=(localdb)\\mssqllocaldb;Database=BankTransferDb;Trusted_Connection=True;"
}
```

### Run it

```bash
dotnet run --project BankTransferService
```

The API docs (Scalar) will be at `http://localhost:5227/scalar/v1` (or `https://localhost:7150` if using the https profile). Health check is at `/health`.

## Seed data

The SQL script comes with a few test accounts:

| # | Owner | Balance | Overdraft | Active | Good for testing |
|---|-------|---------|-----------|--------|-----------------|
| 1001 | Operating North | 5000.00 | 0.00 | Yes | Normal transfers |
| 1002 | Operating South | 1250.00 | 0.00 | Yes | Insufficient funds scenarios |
| 2001 | Private Buffer | 150.00 | 200.00 | Yes | Overdraft transfers |
| 3001 | Savings Vault | 10000.00 | 0.00 | Yes | Larger transfers |
| 9001 | Dormant Account | 800.00 | 0.00 | No | Inactive account errors |

## Tests

The project ships with two test suites — fast unit tests and slower integration tests against a real database.

### Unit tests

```bash
dotnet test BankTransferService.Tests
```

These don't need a database. They cover the pure business rules in `TransferService.ValidateBusinessRules` (active checks, insufficient funds, overdraft) plus the pre-DB validations in `ExecuteTransferAsync` (zero/negative amount, same account). The repositories and connection factory are substituted with NSubstitute, so the rules are tested without any I/O.

### Integration tests

```bash
dotnet test BankTransferService.IntegrationTests
```

**Requires Docker running.** These tests use [Testcontainers](https://dotnet.testcontainers.org/) to spin up a real SQL Server 2022 container, run the project's `database/bank-transfer-service-schema-seed.sql` against it, and exercise `TransferService` end-to-end. Each test resets the schema so they stay isolated. First run pulls the mssql image (~1.5 GB, takes a minute or so); subsequent runs complete in ~10 seconds total.

These cover the parts that mocks can't honestly verify — row-level locking, transaction commit, rollback when a failure happens after balances have been touched, and idempotency-key replay behaviour.

### Test scenarios

| ID | Scenario | Input | Expected | DB effect | Coverage |
|----|----------|-------|----------|-----------|----------|
| T01 | Valid transfer | 1001→1002, 100.00 | 201 | Balances updated, transfer logged | Integration |
| T02 | Overdraft transfer | 2001→1001, 300.00 | 201 | 2001 goes to -150.00 (within limit) | Integration |
| T03 | Insufficient funds | 1002→1001, 1300.00 | 400 | Nothing changes | Integration + unit |
| T04 | Same account | 1001→1001, 50.00 | 400 | Nothing changes | Unit |
| T05 | Zero amount | 1001→1002, 0.00 | 400 | Nothing changes | Unit |
| T06 | Negative amount | 1001→1002, -25.00 | 400 | Nothing changes | Unit |
| T07 | Unknown sender | ???→1002, 50.00 | 404 | Nothing changes | Integration |
| T08 | Unknown receiver | 1001→???, 50.00 | 404 | Nothing changes | Manual |
| T09 | Inactive sender | 9001→1001, 50.00 | 400 | Nothing changes | Integration + unit |
| T10 | Decimal amount | 1001→1002, 99.95 | 201 | Balances reflect exact decimals | Manual |
| T11 | SQL injection attempt | Malicious reference string | 201/400 | Tables intact, parameterized SQL handles it | Manual |
| T12 | Mid-transaction failure | Simulated error after debit | 500 | Full rollback, no changes | Integration |
| T13 | Idempotent retry | Same `Idempotency-Key` twice | 201 both times, same `transferId` | Balances change once, one transfer row | Integration |

## Continuous integration

A GitHub Actions workflow at `.github/workflows/ci.yml` runs on every push and pull request. It restores, builds in Release configuration, runs the unit tests, and runs the integration tests. The integration step works in CI because GitHub-hosted Ubuntu runners have Docker available, so Testcontainers can spin up the mssql container the same way it does locally.

## Known limitations

This is a school project, so a few things that a real system would need are out of scope:

- No authentication on any endpoint — any caller can move money. A real deployment would need JWT or similar.
- No rate limiting, so a single client could spam transfers.
- Error messages are pretty specific ("account not active") which is great for debugging but leaks internal state. Production would want generic messages plus structured logs for operators.
- No daily transfer limits or per-transaction caps — a single call can drain an account.
- Transfers are logged in the database but there's no audit trail for *who* actually triggered the request (i.e. no actor / API client identity).

---

## AI disclosure

I used AI tools (Claude) to help with parts of the code and documentation.
