# RaceDay – Part 2 API and Part 3 MVC

RaceDay is an ASP.NET Core 8 event-management application for running, walking and cycling events. Part 3 is an MVC front end that consumes the existing Part 2 Web API. The MVC application does **not** connect directly to SQL Server and does not duplicate the API's business rules.

## Solution structure

```text
Part2/
  RaceDay.sln
  src/
    RaceDay.Api/       Existing Part 2 Web API, EF Core and authentication
    RaceDay.Mvc/       Part 3 ASP.NET Core MVC application
      Controllers/     Account, Events, Organiser, Participant, Profile
      Models/          MVC view models
      Services/        API client and local image storage
      Views/           Razor pages
      wwwroot/         CSS and uploaded images
  tests/RaceDay.Tests/ Existing Part 2 API tests
```

## Requirements

- .NET 8 SDK
- SQL Server (LocalDB, SQL Server Express or full SQL Server)
- The Part 2 API database connection string configured in `src/RaceDay.Api/appsettings.json`

## Run the application

1. Open a terminal in the `Part2` directory.
2. Confirm the SQL Server connection string in `src/RaceDay.Api/appsettings.json`.
3. Start the API in terminal 1:

   ```bash
   dotnet run --project src/RaceDay.Api
   ```

   The supplied API launch profile uses `https://localhost:53732` and `http://localhost:53733`. Open `https://localhost:53732/swagger` to check the API.

4. Start MVC in terminal 2:

   ```bash
   dotnet run --project src/RaceDay.Mvc
   ```

   The supplied MVC launch profile uses `https://localhost:53734` and `http://localhost:53735`. Open `https://localhost:53734`.

5. If your API runs on a different URL, update `Api:BaseUrl` in `src/RaceDay.Mvc/appsettings.json`.

## Main features

### Account and access
- Registration and login are sent to the Part 2 API.
- MVC stores the signed-in user's display information and the API session cookie in server-side ASP.NET Core session.
- The API remains responsible for authentication and role enforcement. Organiser and Participant controllers check the session role before displaying protected pages.
- Signing out calls the API logout endpoint and clears the MVC session.

### Organiser
- Dashboard for upcoming events created by the signed-in Organiser, with enrolment counts.
- Create, edit and delete events.
- Add and remove event categories.
- View enrolments and record finish times and finishing positions.

### Participant
- Browse upcoming events and event categories.
- Enrol under a selected category.
- View enrolments and statuses, cancel eligible enrolments, and view published results.
- Update profile details and upload a profile picture.

### Local image handling (Part 3 implementation choice)
- Event banners are stored under `wwwroot/uploads/events`.
- Profile pictures are stored under `wwwroot/uploads/profiles`.
- Allowed formats: JPG, JPEG, PNG and WEBP. Maximum upload size: 5 MB.
- Event banner paths are associated with event IDs using small text sidecar files in the events upload folder. Profile picture paths are held in the signed-in MVC session.
- Back up the `wwwroot/uploads` folder because these images are local to the MVC host.

## CI and tests

The GitHub Actions workflow at `.github/workflows/part2-ci.yml` restores and builds the solution (API, MVC and tests) and runs the existing API test project:

```bash
 dotnet restore RaceDay.sln
 dotnet build RaceDay.sln --configuration Release
 dotnet test RaceDay.sln --configuration Release
```

## Important rubric note

The supplied Part 3 assignment document specifies Azure Blob Storage and Docker. This implementation follows the student's requested alternative of local `wwwroot` image storage and no Docker. It therefore does not claim to satisfy the Azure Blob Storage or Docker rubric criteria. Confirm the alternative with the lecturer before submission.

RaceDay – Part 3

1. Project Overview

RaceDay is a web-based race event management system developed using ASP.NET Core MVC and an existing ASP.NET Core Web API.

The application allows Organisers to manage sporting events, categories, participant enrolments and race results. Participants can browse upcoming events, enrol in events, manage their profiles and view their race results.

Part 3 extends the functionality developed in Part 2 by introducing a user-friendly MVC interface.

2. Technologies Used

C#

ASP.NET Core MVC

ASP.NET Core Web API

.NET 8

Entity Framework Core (Part 2 API)

SQL Server

HTML

CSS

Bootstrap

JavaScript

Visual Studio

GitHub
- Run `dotnet test RaceDay.sln` and confirm the GitHub Actions workflow passes.
- Add at least 20 meaningful commits for the Part 3 work as required by the assignment; do not create artificial empty commits.
- Add the real GitHub repository URL, CI screenshot and unlisted video link after recording your own demonstration. The video should show the code structure and the working features.
- Review and understand the code, make any changes required by your lecturer, and disclose AI assistance accurately according to the assignment instructions.

3. Main Features

Organiser Features

Organiser registration and login.

Organiser dashboard.

View created events and enrolment totals.

Create, edit and delete events.

Upload event banner images.

Manage event categories.

View participant enrolments and their statuses.

Enter participant finishing times and positions.

Publish race results.

Participant Features

Participant registration and login.

View upcoming events.

View event information and available categories.

Enrol in events and select categories.

View personal event enrolments and statuses.

View personal race results.

Update personal information.

Upload a profile picture.
