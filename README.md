# BankTransferService

A small ASP.NET Core Web API that handles money transfers between bank accounts. Built as a portfolio assignment for my S4.1 course — the focus is on clean backend architecture, raw SQL (no ORM), and proper transaction handling.

## What it does

The API lets you transfer money between accounts, look up account info, and view transfer history. All the transfer logic runs inside a single database transaction so either everything goes through or nothing does.

### Endpoints

| Method | Route | What it does |
|--------|-------|-------------|
| `POST` | `/api/transfers` | Transfer money between two accounts |
| `GET` | `/api/accounts/{id}` | Get info about an account |
| `GET` | `/api/accounts/{id}/transfers` | Get transfer history for an account |

**Example POST body:**

```json
{
  "fromAccountId": "11111111-1111-1111-1111-111111111111",
  "toAccountId":   "22222222-2222-2222-2222-222222222222",
  "amount": 100.00,
  "reference": "Invoice 2026-1007",
  "description": "Transfer for test run"
}
```

## Project structure

```
BankTransferService/
├── Controllers/       → Thin HTTP layer, just maps requests to services
├── Services/          → Business rules and transfer logic
├── Data/              → Repositories with parameterized SQL (ADO.NET)
├── Interfaces/        → Contracts for DI
├── Models/            → Domain objects and DTOs
└── database/          → SQL schema + seed data

BankTransferService.Tests/
└── TransferServiceTests.cs
```

The project follows a pretty standard layered setup — controllers don't touch any business logic, services handle the rules, and repositories deal with the database. Everything is wired up through dependency injection with interfaces so it's easy to swap things out for testing.

## Transfer rules

Before a transfer goes through, the service checks:

- Amount has to be greater than 0
- Can't transfer to the same account
- Both accounts need to exist and be active
- Sender can't go below their overdraft limit

If any of those fail, nothing gets saved. The debit, credit, and transfer log all happen in one transaction.

## Getting started

**You'll need:**
- .NET 8 SDK
- SQL Server (LocalDB works fine)

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

The API docs (Scalar) will be at `http://localhost:5227/scalar/v1` (or `https://localhost:7150` if using the https profile).

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

```bash
dotnet test BankTransferService.Tests
```

Unit tests use NSubstitute to mock the repositories so no database is needed. They cover the main validation scenarios — zero/negative amounts, same account, missing accounts, inactive accounts, insufficient funds, overdraft limits, and error handling.

### Manual test scenarios

These are the scenarios I tested manually through Scalar/Postman:

| ID | Scenario | Input | Expected | DB effect |
|----|----------|-------|----------|-----------|
| T01 | Valid transfer | 1001→1002, 100.00 | 201 | Balances updated, transfer logged |
| T02 | Overdraft transfer | 2001→1001, 300.00 | 201 | 2001 goes to -150.00 (within limit) |
| T03 | Insufficient funds | 1002→1001, 1300.00 | 400 | Nothing changes |
| T04 | Same account | 1001→1001, 50.00 | 400 | Nothing changes |
| T05 | Zero amount | 1001→1002, 0.00 | 400 | Nothing changes |
| T06 | Negative amount | 1001→1002, -25.00 | 400 | Nothing changes |
| T07 | Unknown sender | ???→1002, 50.00 | 404 | Nothing changes |
| T08 | Unknown receiver | 1001→???, 50.00 | 404 | Nothing changes |
| T09 | Inactive sender | 9001→1001, 50.00 | 400 | Nothing changes |
| T10 | Decimal amount | 1001→1002, 99.95 | 201 | Balances reflect exact decimals |
| T11 | SQL injection attempt | Malicious reference string | 201/400 | Tables intact, parameterized SQL handles it |
| T12 | Mid-transaction failure | Simulated error after debit | 500 | Full rollback, no changes |

---

## Known limitations

Since this is a school project there are some things I didn't implement but would consider for a real system:

- There's no authentication, so all endpoints are open. You'd obviously want JWT or something similar in production.
- No rate limiting on any of the endpoints, so in theory someone could just spam transfers.
- The API gives pretty specific error messages like "account not active" which is nice for debugging but probably not great in production since it leaks internal state.
- No daily transfer limits or per-transaction caps — you can drain a whole account in one call.
- Transfers are logged in the database but there's no audit trail for who actually made the request.

---

## AI disclosure

I used AI tools (Claude) to help with parts of the code and documentation.
