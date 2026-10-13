# Bean Scene reservation system

A restaurant reservation system for a fictional cafe and restaurant, built with **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core**, **SQL Server** and **ASP.NET Core Identity**.

Guests book a table online, staff manage and confirm reservations and assign tables, and managers set up sittings, look after accounts and view reports. It was built as part of the ICT50220 Diploma of Information Technology at TAFE NSW.

<!--
Screenshots: save images in docs/screenshots/ and delete the comment markers around the images to show them.

![Booking form](docs/screenshots/booking-form.png)
![Staff reservations](docs/screenshots/reservations.png)
![Table allocation](docs/screenshots/allocation.png)
![Accounts](docs/screenshots/accounts.png)
-->

## What it does

**Guests and members**
- Book a table by picking a date, a sitting (Breakfast, Lunch or Dinner), a start time, a duration and the number of guests.
- The form only offers valid choices: start times stay inside the sitting, a booking must finish before the sitting ends, and times that have already passed are blocked.
- Register as a member so your details are filled in automatically, then view and cancel your own reservations.
- Registration shows a demonstration-website notice and needs a tick to confirm you aren't using real personal details.

- **My account** page: update your name and phone number, change your email address (needs your current password), or delete your account. Deleting cancels upcoming bookings and removes your personal details from your booking history.
- Australian phone number validation (`0412 345 678`, `02 9876 5432` or `+61` format). Numbers are saved in one format (`0412345678`).
- One guest cannot hold two overlapping bookings on the same sitting.

**Staff**
- Create, edit and search reservations taken in person, by phone or by email. The search matches guest name, email or phone number.
- Move a reservation through its life cycle: Pending, Confirmed, Seated, Completed or Cancelled.
- Assign one or more tables from the Main, Outside and Balcony areas (10 tables each). A table cannot be given to two overlapping reservations.
- Follow-up list of pending reservations that need confirming.

**Managers**
- Create, edit, close and delete sittings (closed sittings stop taking bookings).
- **Accounts** page: create staff accounts (a username and password, with the login set to `username@beanscene.test`, and an optional phone number), edit a staff or member's name, phone and email, disable and re-enable logins (history is kept), and reset passwords. Manager accounts are not listed, so a manager cannot lock themselves out.
- Reports, and a REST API for reservations (GET, POST, PUT, DELETE).

## Business rules

- A sitting has a date, a type, opening and closing times and a capacity. Total guests booked cannot go over the capacity.
- A reservation must start inside its sitting and finish before the sitting ends. Duration is 30 to 180 minutes, and new bookings default to 45 minutes.
- New reservations cannot be made for a time that has already passed.
- Roles: **Manager**, **Staff** and **Member**, enforced with `[Authorize(Roles = ...)]`. Only members can register themselves. Staff accounts are created by a manager.
- Concurrent edits are detected with a row version, so two staff members cannot silently overwrite each other.

## Tech

| Area | Choice |
|---|---|
| Web | ASP.NET Core MVC, Razor views, Bootstrap 5, JavaScript |
| Data | Entity Framework Core (code first, migrations), SQL Server LocalDB |
| Security | ASP.NET Core Identity, role-based authorisation, anti-forgery tokens |
| Tests | xUnit with an in-memory SQLite database |

## Run it

**Requirements:** Visual Studio with the *ASP.NET and web development* workload, the .NET 10 SDK, and SQL Server LocalDB (installed with the workload).

1. Open `BeanScene.sln` in Visual Studio. NuGet packages restore automatically.
2. In **Tools > NuGet Package Manager > Package Manager Console**, with `BeanScene.Web` as the Default project and Startup project, run:
```
   Add-Migration InitialCreate
   Update-Database
```
   Command line alternative: `dotnet ef migrations add InitialCreate --project BeanScene.Web`, then `dotnet ef database update --project BeanScene.Web`.
3. Press **F5**. On start-up the app applies migrations and seeds the roles, the 30 tables, demo users and 90 days of sittings. It tops up missing sittings every time it starts.

If you skip the migration, the app creates the schema itself, but a migration is the better way to run it.

## Demo accounts

These are demo logins for trying the app locally. They are not real accounts.

| Role | Email |
|---|---|
| Manager | manager@beanscene.test |
| Staff | staff@beanscene.test |
| Member | member@beanscene.test |

The password for all three is the `Seed:Password` value in `appsettings.json`. You can also register a new member from the Register page.

## Tests

Open **Test > Test Explorer** and choose **Run All**, or run `dotnet test` in the solution folder. The tests cover the booking rules (capacity, closed sittings, times outside the sitting, past times, overlapping bookings), table allocation, concurrency handling and the data structures. They need no SQL Server.

## Limits

- The app does not send emails, so there is no "forgot password" email. A manager resets a staff or member password from the Accounts page and passes the new password on in person or by phone. Email changes are not verified by a confirmation email.
- A disabled account that is already logged in stays logged in until its login is next checked (up to about 30 minutes).
- It is a course project that runs locally. It is not hosted.
- The login page has a "Forgot your password?" link, but it only opens an under-construction page that tells users to contact the manager.

## More documentation

- `docs/ERD.md`: entity relationship diagram and class diagram
- `docs/DATA_DICTIONARY.md`: tables, columns and constraints
- `docs/api-requests.http`: example REST API requests