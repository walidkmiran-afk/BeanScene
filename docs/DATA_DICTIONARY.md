# Data dictionary

Types are shown as C# type / SQL Server column type. All tables below (except the join table's own columns noted) also contain the three columns from `BaseEntity`:

| Column | Type | Description |
|---|---|---|
| CreatedAt | DateTime / datetime2 | Set when the row is first saved |
| UpdatedAt | DateTime / datetime2 | Set every time the row is saved |
| RowVersion | Guid / uniqueidentifier | Optimistic concurrency token (`[ConcurrencyCheck]`); a new value is generated on every insert and update. Two people editing the same row cannot silently overwrite each other |

## Areas

| Column | Type | Constraints | Description |
|---|---|---|---|
| AreaId | int | PK, identity | Area key |
| AreaName | string / nvarchar(50) | Required, unique | Main, Outside or Balcony |

## Tables

| Column | Type | Constraints | Description |
|---|---|---|---|
| TableId | int | PK, identity | Table key |
| TableName | string / nvarchar(10) | Required, unique | M1-M10, O1-O10, B1-B10 |
| AreaId | int | FK to Areas, restrict delete | Area the table is in |
| Capacity | int | 1-50 | Seats at the table |

## Sittings

| Column | Type | Constraints | Description |
|---|---|---|---|
| SittingId | int | PK, identity | Sitting key |
| SittingType | string / nvarchar(20) | Enum: Breakfast, Lunch, Dinner, Special | Kind of sitting |
| Name | string / nvarchar | Required | The sitting's name. A manager can rename a sitting for one day in the day editor. A booking made under the old name shows the old name crossed out beside the new one |
| SittingDate | DateOnly / date | Required | Day of the sitting |
| StartTime | TimeOnly / time | Required | Opening time |
| EndTime | TimeOnly / time | Required, after StartTime | Closing time. Open sittings on the same day cannot overlap |
| Capacity | int | 1-500 | Maximum guests. Total guests booked cannot go over it |
| IsClosed | bool / bit | | Closed to new reservations |
| ClosedReason | string / nvarchar(20) | Enum: None, BookedOut, PrivateEvent | Why it is closed |

A day that has passed is read-only. This is checked on the server as well as on the page.

## Customers

| Column | Type | Constraints | Description |
|---|---|---|---|
| CustomerId | int | PK, identity | Customer key |
| FirstName | string / nvarchar(60) | Required | A guest only has to give one name, so a first name is enough |
| LastName | string / nvarchar(60) | Optional for guests | Members give a first and last name when they sign up |
| Email | string / nvarchar(256) | Optional for guests, valid format when given, indexed | Used to match a booking to an earlier guest or a member |
| PhoneNumber | string / nvarchar(30) | Required when booking, Australian format | Used so the restaurant can call the guest, and to match earlier guests |
| UserId | string / nvarchar(450) | FK to AspNetUsers, nullable, unique | Set when the customer is a registered member (one-to-one) |

When a member account is deleted, the login is removed, upcoming bookings are cancelled, and the person's name, email and phone are removed from past bookings. The bookings stay so reports still add up.

## Reservations

| Column | Type | Constraints | Description |
|---|---|---|---|
| ReservationId | int | PK, identity | Reservation key |
| SittingId | int | FK to Sittings, restrict delete | Sitting booked. Can change if sittings change and another open sitting covers the start time |
| CustomerId | int | FK to Customers, restrict delete | Guest |
| StartTime | TimeOnly / time | Within the sitting, 15 minute steps, up to 30 minutes before the sitting ends | When the guests arrive. Never changes when sittings change |
| DurationMinutes | int | 15-480 in the model, 30-180 on the booking forms | Length of the booking. It must finish before the restaurant closes |
| NumberOfGuests | int | 1-100 | Party size |
| Source | string / nvarchar(20) | Enum: Online, Mobile, InPerson, Email, Phone | How it was requested |
| Status | string / nvarchar(20) | Enum: Pending, Confirmed, Cancelled, Seated, Completed | Workflow state, changed only by `Confirm`, `Cancel`, `Seat`, `Complete` and `Undo` (one step back) |
| SpecialRequests | string / nvarchar(500) | Nullable | Notes such as "highchair required" |

**Status rules**
- Pending -> Confirmed -> Seated -> Completed, or Cancelled from Pending or Confirmed.
- Seat is allowed from Pending or Confirmed, but only when at least one table is assigned.
- Undo goes one step back: Confirmed -> Pending, Seated -> Confirmed, Completed -> Seated. It is not allowed on a day that has passed.
- A cancelled booking cannot be brought back. A new booking has to be made.
- **Past** is not stored. A Pending or Confirmed booking whose sitting date is before today is shown as past, worked out from the date each time, so a wrong clock cannot change a booking.
- The same phone number or the same email cannot hold two overlapping bookings in one sitting.

## ReservationTables (join table)

| Column | Type | Constraints | Description |
|---|---|---|---|
| ReservationId | int | PK part 1, FK to Reservations, cascade delete | |
| TableId | int | PK part 2, FK to Tables, restrict delete | |

A table cannot be given to two reservations that overlap in time in the same sitting.

## ASP.NET Core Identity tables

`AspNetUsers` (login accounts), `AspNetRoles` (Manager, Staff, Member) and `AspNetUserRoles` are created by Identity. Passwords are stored only as hashes (`PasswordHash`). A staff login is a username plus `@beanscene.test`. Members log in with their email. A disabled account cannot log in, and its history is kept.