# Bean Scene reservation system

A restaurant reservation system for a fictional cafe and restaurant, built with **ASP.NET Core MVC (.NET 10)**, **Entity Framework Core**, **SQL Server** and **ASP.NET Core Identity**.

Guests book a table online, staff manage and confirm reservations and assign tables, and managers set up sittings, look after accounts and view reports. It was built as part of the ICT50220 Diploma of Information Technology at TAFE NSW.

<!--
Screenshots: save images in docs/screenshots/ and delete the comment markers around the images to show them.

![Booking form](docs/screenshots/booking-form.png)
![Sittings page](docs/screenshots/sittings.png)
![Staff reservations](docs/screenshots/reservations.png)
-->

## Design approach

- **Intuitive.** Nothing needs instructions. The page shows what you can do, and only offers choices that are valid.
- **No page changes for small jobs.** Editing, confirming, assigning tables, saving a day, adding a booking and managing accounts all happen in place. There are no pop-up windows, and nothing blocks the screen or closes by itself.
- **Staff stay on the page, customers are slowed down on purpose.** Staff enter many bookings in a row, so their booking page stays put and clears itself for the next one. Customers don't need to make bookings rapidly, so they are not given that option. After a customer books, they are taken to a confirmation page that shows what they booked. They have to start a new booking from scratch if they want another one. Signing up works the same way. The customer goes to the homepage with a welcome message instead of staying on the form.
- **One tool, used everywhere.** The booking buttons (Edit, Tables, Confirm, Seat, Complete, Cancel, Delete) are a single shared panel. The Sittings, Reservations and Follow-up pages all use it, so it looks and behaves the same everywhere. There are no separate Edit, Delete or Create pages. The one exception is Book a table.
- **Several ways to do the same job.** Book a table is on the homepage and in the menu, and managers can also add a booking from a sitting on the Sittings page. Tables can be assigned from any booking list or from the Allocation page. Each person can use the way they are comfortable with.
- **Friendly on every standard screen.** The pages are built to work in a desktop, laptop or tablet browser. Tables drop less important columns, and rows of buttons re-flow as the window gets smaller.

## What it does

**Guests and members**
- Book a table with an always-open month calendar, then a sitting, a start time, a duration and the number of guests. The form opens on the next available date and sitting. Days with no room are greyed out.
- The start time stays as chosen. When you pick a new date, the sitting that covers that time is selected for you. If no sitting covers it, nothing else is picked for you: the sitting is left blank with a note, and only the available choices are listed.
- Start times are in 15 minute steps. A booking can start up to 30 minutes before its sitting ends, and must finish before the restaurant closes. Times that have already passed are blocked.
- A guest only has to give one name (a first name is enough) and a phone number so the restaurant can call them. Email is optional. A logged-in member's name is filled in for them. Signing up as a member asks for a first and last name.
- Register as a member and your details are filled in for you. The booking form doesn't show the name, email and phone boxes at all for a logged-in member. You can also view and cancel your own reservations.
- Sittings that were renamed after you booked show the old name crossed out with the new name and a grey "Changed" tag.
- Australian phone number validation (`0412 345 678`, `02 9876 5432` or `+61` format).
- The same phone number or the same email address cannot hold two overlapping bookings on one sitting. The message says which one matched.

**Staff**
- Book a table for a guest (in person, by phone or by email). Staff choose the source and stay on the page after each booking, so bookings can be entered one after another.
- Every booking checks the phone number and the email against existing customers. Staff are told whether the guest matched a member account or an earlier guest, and by which one. A phone number alone never attaches a booking to a member account, and the same number with a different name is kept as a separate guest.
- Reservations list with search, grouped by day and by sitting. Dates, status, source and table search as soon as you change them, and a typed name searches on Enter. Every booking has the shared panel: Edit and Tables open inline, and Confirm, Seat, Complete, Cancel and Delete (managers only) save without reloading the page.
- Follow-up list of pending reservations, soonest first, with the same search and the same panel. It is ordered with a merge sort held in a doubly linked list.
- Assign one or more tables from the Main, Outside and Balcony areas (10 tables each). A table cannot be given to two overlapping reservations.

**Managers**
- **Sittings page.** A weekly hours editor (open or closed, from, to and seats for Breakfast, Lunch and Dinner on each day of the week) with an All column that sets a meal on all seven days at once, where times can be typed short (12 becomes 12:00), and a month calendar showing each day's sittings and number of bookings. Click a day to open it in place.
- **Day editor.** For any day: rename sittings, open or close each one, change times and seats, add a Special sitting, close the whole day or reset it to the weekly hours. Reset also removes any Special sittings. Their bookings move to the regular sitting that covers their time, or are cancelled if none does.
- Time boxes in the weekly hours and the day editor work the same way, with its bookings, but not changed. Its boxes are locked and the save and reset buttons are hidden, and the server refuses changes to it too.
- Time boxes in the weekly hours and the day editor work the same way. Type 12 or 930 and it becomes 12:00 or 09:30. Clicking into a box selects it, so what you type replaces it. A box that can't be understood turns red as soon as you leave it. A sitting that ends before it starts, or overlaps another sitting that day, turns red straight away too. Nothing saves until it's fixed.
- **Bookings of a sitting.** Click a sitting's Bookings number, even when it is 0, to open its bookings with the shared panel and an **Add booking** form that offers only the valid start times and durations.
- If a change leaves bookings outside every open sitting, the page lists them and asks for confirmation before they are cancelled. Other bookings move to the sitting that now covers their start time.
- Weekly changes update only days that have not been customised by hand.
- **Accounts page.** Staff and Members are in separate tabs with counts, and one search box finds an account by name, email or phone as you type. New staff accounts, editing, password resets, enable or disable and delete all happen on the same page. A disabled account cannot log in, and its history is kept. Deleting an account removes the login, cancels upcoming bookings and removes the person's name, email and phone from past bookings, which stay for reports.
- A member's row on the Accounts page also has **Bookings** and **Book** buttons. Bookings drops open every reservation the member has, upcoming first and then the history, with the same shared booking buttons (Edit, Tables, Confirm, Seat, Complete, Cancel, Delete). Book drops open the booking form with the member's details filled in and locked. Both open under the member's row, one panel at a time, and nothing leaves the page.
- Reports update as soon as you change the From or To date, and a REST API for reservations (GET, POST, PUT, DELETE).

## Business rules

- A sitting has a date, a name, opening and closing times, a capacity and an open or closed state. Total guests booked cannot go over the capacity.
- A booking starts inside its sitting, in 15 minute steps, up to 30 minutes before the sitting ends. It must finish before the restaurant closes (the end of the day's last open sitting). Duration is 30 to 180 minutes.
- New reservations cannot be made for a time that has already passed.
- A booking's time never changes when sittings change. Only its sitting can change, if another open sitting covers its start time.
- A cancelled booking cannot be brought back, because its time and tables are free for someone else. A new booking has to be made.
- A Pending or Confirmed booking from a day that has passed
  shows a grey **Past** tag and its buttons are disabled. This
  is worked out from the date and is never stored, so a wrong
  clock can't change a booking.
- Email addresses must be real in format, like name@example.com.
  The same rule applies on sign-up, change email, the manager's
  edit form and the booking form.
- A staff login is a username plus `@beanscene.test`. A manager can change a staff member's username, but never their email, and the username must not already be taken. Members log in with their email, so a manager can change that instead.
- Roles: **Manager**, **Staff** and **Member**, enforced with `[Authorize(Roles = ...)]`.
- Concurrent edits are detected with a row version, so two staff members cannot silently overwrite each other.

## How the time and seats boxes work

The weekly hours table and the day editor on the Sittings page are full of small time and seats boxes. They all follow the same rules, so those rules live in one file, `wwwroot/js/time-boxes.js`, instead of being copied into each panel.

**What the rules are**
- A short time is understood: `12` becomes `12:00`, `9` becomes `09:00` and `930` becomes `09:30`.
- Seats must be a whole number from 1 to 500.
- Clicking or tabbing into a box selects everything in it, so what you type replaces it.
- The browser's own format message is switched off. A box that can't be understood turns red instead, as soon as you leave it.
- An open sitting that ends before it starts, or overlaps another open sitting on the same day, turns red as soon as you finish a box or press Open / Closed.
- Nothing is saved while any box is red. The server checks the same rules again, so the page is never the only protection.

**How a panel uses it**

`sittings.js` makes one call for each panel:

```js
wireTimeBoxes(form, '.box-class', { isTime, onTidy, groups });
```

- `form` and `'.box-class'` say which boxes the rules apply to.
- `isTime` says which boxes are times and which are seats. The default is a box whose name contains "Time".
- `onTidy` runs after a box is tidied. The weekly hours use it to copy an All box into the seven days.
- `groups` says where the open sittings are, one list per day, so the page can check that they don't clash. Each panel knows its own layout, so each gives its own `groups`.

**Where it is used**
- **Weekly hours:** the seven day boxes for each meal, and the three All boxes.
- **Day editor:** the boxes of every row, including rows added with Add sitting.
- **Add booking panel:** not yet. It could use the same rules with one more call.

Because there is one copy, a change to a rule (for example a minimum gap between sittings) is made once, in `time-boxes.js`, and both panels get it.

## Tech

| Area | Choice |
|---|---|
| Web | ASP.NET Core MVC, Razor views and partial views, Bootstrap 5, plain JavaScript (`fetch`) for in-place updates |
| Data | Entity Framework Core (code first, migrations), SQL Server LocalDB |
| Security | ASP.NET Core Identity, role-based authorisation, anti-forgery tokens |
| Tests | xUnit with an in-memory SQLite database |

Shared pieces live in `Views/Shared` (`_BookingActions`, `_ReservationSearch`, `_ReservationFields`, `_SittingLabel`) and `wwwroot/js` (`booking-actions.js`, `sittings.js`, `time-boxes.js`, `add-booking.js`, `accounts.js`). `time-boxes.js` holds the time and seats rules shared by the weekly hours and the day editor.

## Run it

**Requirements:** Visual Studio with the *ASP.NET and web development* workload, the .NET 10 SDK, and SQL Server LocalDB (installed with the workload).

1. Open `BeanScene.sln` in Visual Studio. NuGet packages restore automatically.
2. Press **F5**. On start-up the app applies the migrations and seeds the roles, the 30 tables, demo users and 90 days of sittings. It tops up missing sittings every time it starts.

To apply the migrations by hand, open **Tools > NuGet Package Manager > Package Manager Console**, with `BeanScene.Web` as the Default project and Startup project, and run `Update-Database`. Command line alternative: `dotnet ef database update --project BeanScene.Web`.

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

## More documentation

- `docs/ERD.md`: entity relationship diagram and class diagram
- `docs/DATA_DICTIONARY.md`: tables, columns and constraints
- `docs/WALKTHROUGH.md`: walkthrough of the app
- `docs/api-requests.http`: example REST API requests