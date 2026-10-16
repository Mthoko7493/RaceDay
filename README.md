# RaceDay API - Part 2

RESTful ASP.NET Core 8 Web API for **RaceDay**, an event management platform for South African road running, walking and cycling events. Built to the Part 1 endpoint plan, using Entity Framework Core (Code-First) on SQL Server, server-side session authentication, Swagger, and xUnit tests run through GitHub Actions.

- **Part 1 docs:** see `/docs` (ERD, endpoint plan, SQL script)
- **YouTube video (unlisted):** `<paste link here>`
- **CI/CD green build:** `<paste screenshot here, e.g. ![CI](docs/ci-part2.png)>`

## Roles

| Role | Can do |
|---|---|
| **Organiser** | Create, edit and delete their own events; add/edit/delete categories on their events; view all enrolments for their events; capture finish times and positions. |
| **Participant** | Browse events and categories; enrol in an event under a category; view and cancel their own enrolments; view their own results. |
| Both | Register, log in/out, view and update their own profile. Public (no login): list events, event detail, categories, event results. |

Role checks are enforced on the server by `SessionAuthorizeAttribute`: **401** when there is no session, **403** when the role is wrong or the resource belongs to another Organiser/Participant.

## Project layout

```
Part2/
  RaceDay.sln
  src/RaceDay.Api/            ASP.NET Core Web API
    Controllers/              Auth, Profile, Events, Categories, Enrolments, Results
    Data/RaceDayDbContext.cs  EF Core model (mirrors the Part 1 ERD / SQL script)
    Models/Entities.cs        Role, User, Event, Category, Enrolment, Result
    Dtos/Dtos.cs              Request/response contracts + mappers
    Security/                 PBKDF2 PasswordHasher, session authorisation filter
  tests/RaceDay.Tests/        xUnit tests (WebApplicationFactory, in-memory DB)
.github/workflows/part2-ci.yml   Build + test on every push
```

## Setup and run

Prerequisites: .NET 8 SDK and SQL Server (LocalDB, Express or full).

1. Check the connection string in `src/RaceDay.Api/appsettings.json` (default: LocalDB, database `RaceDayDB`).
2. Database - pick **one**:
   - **Let EF create it:** just run the API. On startup `EnsureCreated()` builds the schema if the database is empty and seeds the two roles.
   - **Use the Part 1 script:** run `docs/RaceDay.sql` in SSMS first. The API detects the existing tables and leaves them alone.
3. Run:
   ```bash
   cd Part2
   dotnet restore
   dotnet run --project src/RaceDay.Api
   ```
4. Open the URL printed in the console, `/swagger` opens automatically from `/`.

> **Seed users from the SQL script:** their `PasswordHash` values must be in this API's format (`v1.<iterations>.<salt>.<hash>`) to log in. If they are placeholders, register fresh users through `POST /api/auth/register`.

### Using Swagger
1. `POST /api/auth/register` (choose `Organiser` or `Participant`), then `POST /api/auth/login`.
2. Swagger UI is same-origin, so the session cookie (`.RaceDay.Session`) is sent automatically on later calls.
3. `POST /api/auth/logout` ends the session. To switch role, log out and log in as the other user.

## Authentication design
- Passwords are hashed with PBKDF2-SHA256 (100,000 iterations, random 16-byte salt) and never stored or returned in plain text.
- Login stores `UserId` and `Role` in a **server-side session**; the browser only holds an HttpOnly session cookie (30 min idle timeout). The session is cleared first on login (prevents session fixation) and on logout.
- Role selection at registration follows the Part 1 plan (`role` in the request body). In a production system, Organiser accounts would need an approval step or invite code, otherwise anyone can self-register as an Organiser.

## Endpoints (21, same as the Part 1 plan)

| Method | Route | Role |
|---|---|---|
| POST | `/api/auth/register` | None |
| POST | `/api/auth/login` | None |
| POST | `/api/auth/logout` | Any |
| GET / PUT | `/api/profile` | Any |
| GET | `/api/events` | None |
| GET | `/api/events/{id}` | None |
| POST | `/api/events` | Organiser |
| PUT / DELETE | `/api/events/{id}` | Organiser (owner) |
| GET | `/api/events/{eventId}/categories` | None |
| POST | `/api/events/{eventId}/categories` | Organiser (owner) |
| PUT / DELETE | `/api/categories/{id}` | Organiser (owner) |
| POST | `/api/events/{eventId}/enrolments` | Participant |
| GET | `/api/enrolments/my` | Participant |
| GET | `/api/events/{eventId}/enrolments` | Organiser (owner) |
| DELETE | `/api/enrolments/{id}` | Participant (owner) |
| POST | `/api/enrolments/{enrolmentId}/result` | Organiser (owner) |
| GET | `/api/results/my` | Participant |
| GET | `/api/events/{eventId}/results` | None |

### Small additions beyond the plan (explained)
- **401 responses** are returned by every protected endpoint (the plan only listed them on some), and **400** for validation failures.
- **Cancelling an enrolment** sets its status to `Cancelled` instead of deleting the row (the schema has a unique `(ParticipantID, EventID)` constraint and a `Cancelled` status). Enrolling again reactivates it as `Pending`. A cancelled enrolment cannot receive a result, and an enrolment that already has a result cannot be cancelled (409).
- **Recording a result** sets the enrolment status to `Confirmed`.
- **Result responses include `totalFinishers`** so the Part 3 UI can show "47th out of 312".
- **Deleting a category** that has enrolments returns 409. **Deleting an event** also removes its categories, enrolments and results.
- Enrolling in an event whose date has passed returns 400.

## Unit tests

```bash
cd Part2
dotnet test
```

The tests boot the real API through `WebApplicationFactory` with `ASPNETCORE_ENVIRONMENT=Testing`, which swaps SQL Server for an isolated in-memory database, and use cookie-keeping `HttpClient`s so the session works exactly as it does in a browser. Coverage:

| File | What it checks |
|---|---|
| `AuthenticationTests` | Register (201/400/409), password stored hashed, login success/failure, profile get/update, logout ends the session |
| `EventTests` | Organiser create/update/delete, ownership (403), validation (400), public listing, upcoming-only filter, 404s |
| `RoleEnforcementTests` | Participant blocked from Organiser actions (403), Organiser blocked from Participant actions (403), every protected endpoint returns 401 without a session |
| `EnrolmentTests` | Enrol (201, Pending, recorded), duplicate (409), wrong event/category (404), past event (400), organiser view, cancel, category rules |
| `ResultTests` | Record result, duplicate (409), non-owner (403), Participant sees only own results, public ordered event results |
| `PasswordHasherTests` | Hash/verify, salting, malformed hashes |

## CI/CD

Race Results

Record race results.

View results for an event.

Retrieve a participant's results.
`.github/workflows/part2-ci.yml` restores, builds (Release) and runs all tests on every push and pull request.

## AI usage disclosure
`<Edit this to describe honestly how you used AI tools, e.g. "Claude was used to generate a first draft of the controllers and tests, which I reviewed, ran and modified.">`
