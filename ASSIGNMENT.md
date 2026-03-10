# Bank Transfer Service - Opgave

**K3 - opsamlingsopgave**

Portfolio-opgave til S4.1 (.NET / C# / backend / SQL / tests)

| Emne | Fokus | Udleveres med |
|------|-------|---------------|
| Microservice til kontooverførsler | MVC backend, SOLID, parameterized SQL, transaktioner, unit tests og dokumentation | Kravspecifikation, database-script med mock data og testtabel |

## Introduktion

I denne opgave skal du udvikle en lille backend-service, som kan gennemføre pengeoverførsler mellem konti. Opgaven er valgt, fordi den minder om rigtige virksomhedsopgaver: der er forretningsregler, databasearbejde, krav om datasikkerhed og behov for testbar kode.

Løsningen skal laves som en lille ASP.NET Core Web API-løsning med tydelig lagdeling. Frontend er ikke en del af opgaven. Du skal derfor fokusere på backend, datamodel, API-endpoints, validering, transaktioner og tests.

Opgaven samler centrale emner fra S4.1: MVC på backend, microservices, Dependency Injection, kvalitetssikring, dokumentation og best practice. Samtidig skal du arbejde direkte med SQL uden ORM, så du viser, at du forstår hvad der foregår under automatiske frameworks.

**Vigtigt:** Programkode, klassenavne, metodenavne, SQL-objektnavne og kommentarer i kode skal være på engelsk. Opgavetekst og dokumentation må gerne være på dansk.

## Emner der behandles

### Mål

- S4.1: Eleven kan konstruere webapplikationer ved anvendelse af det objektorienterede MVC designmønster.
- S4.1: Eleven kan anvende det objektorienterede designmønster Inversion of Control (IoC) med Dependency Injection.
- S4.1: Eleven kan anvende en IoC Container.
- S4.1: Eleven kan skabe simple microservices.
- S4.1: Eleven kan kvalitetssikre en webapplikation ved anvendelse af best-practice inden for området.
- S4.1: Eleven kan dokumentere og videregive sin viden.

### Hovedemner

- ASP.NET Core Web API med controllers
- SOLID-principperne i service-, repository- og model-lag
- Parameterized SQL uden ORM
- SQL-transaktioner ved pengeoverførsel
- OO kravforståelse og use cases
- Unit tests og enkel kvalitetssikring
- Kodedokumentation og professionel struktur

### Andre emner

- HTTP statuskoder og API-kontrakter
- Validering af inputdata
- Swagger / OpenAPI til hurtig afprøvning
- Readability, naming og clean code

### Forudsætninger

- C# og objektorienteret programmering
- Basal ASP.NET Core og dependency injection
- Klasser, interfaces og exceptions
- Grundlæggende SQL SELECT, INSERT og UPDATE
- Basale unit tests i .NET

## Instruktion

Du skal udvikle en lille microservice med fokus på kontooverførsler. Løsningen skal kunne afprøves via Swagger, Postman eller tilsvarende værktøj. Nedenfor får du først de overordnede rammer og derefter en kravspecifikation i OO-stil med use cases.

### Rammer for løsningen

- Løsningen skal være en ASP.NET Core Web API-løsning bygget omkring controllers.
- Controller-laget skal være tyndt. Forretningsregler skal ligge i services/domænelag.
- Dataadgang skal laves uden ORM. Brug ADO.NET eller Dapper med tydeligt parameterized SQL.
- Selve pengeoverførslen skal gennemføres i én database-transaktion.
- Løsningen skal anvende Dependency Injection og interfaces, så centrale dele kan testes.
- Der skal laves unit tests for de vigtigste regler. Integration tests er tilladt som supplement, men ikke som erstatning for unit tests.
- Repoet skal indeholde en README med kort forklaring af arkitektur, hvordan projektet startes, og hvordan tests køres.

## OO kravspecifikation

### Systemets formål

Systemet skal give andre systemer eller medarbejdere mulighed for at gennemføre sikre pengeoverførsler mellem interne konti. En overførsel må kun gennemføres, hvis den overholder reglerne for kontoens saldo, overdraft og status. Hvis én del af overførslen fejler, må ingen del af overførslen gemmes.

### Aktører

- **API-klient:** et andet system eller en udvikler, som kalder servicen via HTTP.
- **Database:** gemmer konti og overførsler.
- **Udvikler/tester:** bruger Swagger eller tests til at afprøve løsningen.

### Domæneobjekter

| Objekt | Ansvar | Vigtige egenskaber |
|--------|--------|--------------------|
| Account | Repræsenterer en konto, som kan sende eller modtage penge. | Id, AccountNumber, OwnerName, Balance, OverdraftLimit, IsActive |
| Transfer | Repræsenterer en gennemført pengeoverførsel. | Id, FromAccountId, ToAccountId, Amount, Reference, Description, CreatedUtc |
| TransferRequest | Inputmodel fra API-klient ved oprettelse af overførsel. | FromAccountId, ToAccountId, Amount, Reference, Description |
| TransferResult | Resultat fra service-laget til controlleren. | Success/Failure, fejlbesked, evt. transfer id |

### Forretningsregler

- Beløbet skal være større end 0.
- En konto må ikke overføre til sig selv.
- Begge konti skal eksistere.
- Begge konti skal være aktive.
- Afsenderkontoen må ikke ende under sin tilladte overdraft-grænse.
- Ved succes skal afsenderens saldo reduceres, modtagerens saldo øges, og overførslen logges.
- Alle tre handlinger ovenfor skal ske i samme transaktion.
- Ved fejl skal databasen stå uændret.

## Use cases

| ID | Navn | Kort beskrivelse | Primær aktør |
|----|------|-------------------|----------------|
| UC-01 | Gennemfør overførsel | API-klienten sender en overførselsanmodning, og systemet gennemfører overførslen atomisk hvis reglerne overholdes. | API-klient |
| UC-02 | Hent konto | API-klienten henter data for en konto for at se saldo, ejer og status. | API-klient |
| UC-03 | Vis overførselshistorik for konto | API-klienten kan hente tidligere overførsler for en konto. | API-klient |

### UC-01 - Gennemfør overførsel

| | |
|---|---|
| **Kort beskrivelse** | Flytter et beløb fra én konto til en anden og registrerer en transfer-log. |
| **Trigger** | API-klienten kalder POST /api/transfers. |
| **Startbetingelser** | Databasen er tilgængelig. Begge konto-id'er er gyldige GUID-værdier i requesten. |
| **Slutbetingelser ved succes** | Balances er opdateret korrekt og ny transfer er gemt. |
| **Slutbetingelser ved fejl** | Ingen ændringer er gemt i databasen. |
| **Primært forløb** | 1) Request valideres. 2) Konti hentes. 3) Regler kontrolleres. 4) Transaktion startes. 5) Debit/credit udføres. 6) Transfer logges. 7) Commit. 8) API returnerer succes. |
| **Alternative forløb** | Beløb <= 0, samme konto på begge sider, konto mangler, konto er inaktiv, utilstrækkelige midler, databasefejl som skal udløse rollback. |

### UC-02 - Hent konto

| | |
|---|---|
| **Kort beskrivelse** | Returnerer data for en konto ud fra konto-id. |
| **Trigger** | API-klienten kalder GET /api/accounts/{id}. |
| **Startbetingelser** | Konto-id er angivet. |
| **Slutbetingelser ved succes** | Konto-data returneres som JSON. |
| **Slutbetingelser ved fejl** | 404 returneres hvis konto ikke findes. |

### UC-03 - Vis overførselshistorik for konto

| | |
|---|---|
| **Kort beskrivelse** | Returnerer overførsler hvor kontoen enten er afsender eller modtager. |
| **Trigger** | API-klienten kalder GET /api/accounts/{id}/transfers. |
| **Startbetingelser** | Konto-id er angivet. |
| **Slutbetingelser ved succes** | Liste med overførsler returneres sorteret efter nyeste først. |
| **Slutbetingelser ved fejl** | 404 returneres hvis konto ikke findes. |

## Tekniske krav

| Område | Krav |
|---------|------|
| Arkitektur | Løsningen skal være opdelt i mindst Controller, Service og Repository/Data Access. |
| SOLID | Designet skal tydeligt vise SRP og DIP. Interfaces skal bruges meningsfuldt og ikke kun for syns skyld. |
| MVC/backend | Controllers skal modtage requests og returnere HTTP-svar, men ikke selv udføre forretningslogik eller SQL. |
| SQL | Alle SQL-kald skal være parameterized. Dynamisk string-sammensat SQL må ikke bruges til brugerinput. |
| Transaktioner | Overførsel skal udføres atomisk i én database-transaktion. |
| Tests | Der skal mindst laves unit tests for validering og centrale service-regler. |
| Dokumentation | README + korte kodekommentarer/XML-doc, hvor de giver værdi. Kommentarer må ikke erstatte godt navngivne klasser og metoder. |
| Best practice | Brug klare navne, guard clauses, relevante statuskoder, nullable awareness og konsistent fejlhåndtering. |

## Forslag til arbejdsrækkefølge

1. Læs kravspecifikationen igennem og markér de konkrete API-endpoints og regler der skal implementeres.
2. Opret databasen ud fra det vedlagte SQL-script og kontrollér at seed-data er indlæst korrekt.
3. Lav domænemodeller, request/response-modeller og interfaces.
4. Implementér dataadgang med parameterized SQL.
5. Implementér service-laget, herunder transaktion for UC-01.
6. Implementér controllers og relevante statuskoder.
7. Lav unit tests for centrale regler og fejlscenarier.
8. Skriv README og gennemfør manuel test via Swagger/Postman.

## Foreslåede endpoints

| Metode | Rute | Formål | Forventet hovedsvar |
|--------|------|---------|---------------------|
| POST | /api/transfers | Gennemfører en overførsel | 201 Created eller relevant fejl |
| GET | /api/accounts/{id} | Henter én konto | 200 OK eller 404 |
| GET | /api/accounts/{id}/transfers | Henter historik for konto | 200 OK eller 404 |

## Seed-data

| AccountNumber | OwnerName | Balance | Overdraft | IsActive | Brugbar til test af |
|---------------|-----------|---------|-----------|----------|---------------------|
| 1001 | Operating North | 5000.00 | 0.00 | 1 | normal succes, decimalbeløb |
| 1002 | Operating South | 1250.00 | 0.00 | 1 | insufficient funds ved store beløb |
| 2001 | Private Buffer | 150.00 | 200.00 | 1 | tilladt negativ slutbalance inden for limit |
| 3001 | Savings Vault | 10000.00 | 0.00 | 1 | større succesfulde overførsler |
| 9001 | Dormant Account | 800.00 | 0.00 | 0 | inaktiv konto |

## Eksempel på request-body til UC-01

```json
{
  "fromAccountId": "11111111-1111-1111-1111-111111111111",
  "toAccountId":   "22222222-2222-2222-2222-222222222222",
  "amount": 100.00,
  "reference": "Invoice 2026-1007",
  "description": "Transfer for test run"
}
```

## Testtabel

| ID | Scenario | Input | Forventet HTTP | Forventet resultat | DB-effekt |
|----|----------|-------|----------------|--------------------|-----------|
| T01 | Gyldig overførsel | 1001 -> 1002, 100.00 | 201 | Transfer oprettes | 1001 = 4900.00, 1002 = 1350.00, 1 ny transfer |
| T02 | Gyldig overførsel med overdraft | 2001 -> 1001, 300.00 | 201 | Transfer oprettes | 2001 = -150.00, 1001 = 5300.00 |
| T03 | Utilstrækkelige midler | 1002 -> 1001, 1300.00 | 400 | Fejlbesked om funds/limit | Ingen ændringer |
| T04 | Samme konto på begge sider | 1001 -> 1001, 50.00 | 400 | Fejlbesked om same account | Ingen ændringer |
| T05 | Beløb = 0 | 1001 -> 1002, 0.00 | 400 | Fejlbesked om positivt beløb | Ingen ændringer |
| T06 | Negativt beløb | 1001 -> 1002, -25.00 | 400 | Fejlbesked om positivt beløb | Ingen ændringer |
| T07 | Afsender findes ikke | ukendt -> 1002, 50.00 | 404 | Konto ikke fundet | Ingen ændringer |
| T08 | Modtager findes ikke | 1001 -> ukendt, 50.00 | 404 | Konto ikke fundet | Ingen ændringer |
| T09 | Inaktiv afsenderkonto | 9001 -> 1001, 50.00 | 400 | Fejlbesked om konto-status | Ingen ændringer |
| T10 | Decimalbeløb | 1001 -> 1002, 99.95 | 201 | Transfer oprettes | 1001 = 4900.05, 1002 = 1349.95 |
| T11 | Reference ligner SQL-angreb | reference = "x'; DROP TABLE Transfers;--" | 201 eller 400 | Request håndteres sikkert uden SQL-eksekvering | Tabeller består og data er intakte |
| T12 | Fejl midt i transaktion | Simuler fejl efter debit før insert | 500 eller domænefejl | Rollback udføres | Ingen ændringer i balances eller transfers |

## Ekstraopgaver

- Tilføj idempotency key, så samme request ikke kan gennemføre dobbelt overførsel ved gentagne kald.
- Tilføj row version eller anden form for concurrency-beskyttelse.
- Udvid historik-endpoint med filtrering på dato eller minimumsbeløb.
- Lav en integrationstest der beviser rollback ved simuleret fejl midt i transaktionen.

## Aflevering

GitHub-repo med kildekode, tests, README og eventuelle migrations/scripts. SQL-scriptet må gerne ligge i en separat mappe, f.eks. database/ eller sql/.
