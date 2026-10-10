# Data dictionary

Types are shown as C# type / SQL Server column type. All tables below (except the join table's own columns noted) also contain the three columns from `BaseEntity`:

| Column | Type | Description |
|---|---|---|
| CreatedAt | DateTime / datetime2 | Set when the row is first saved |
| UpdatedAt | DateTime / datetime2 | Set every time the row is saved |
| RowVersion | Guid / uniqueidentifier | Optimistic concurrency token (`[ConcurrencyCheck]`); a new value is generated on every insert and update |

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
| SittingDate | DateOnly / date | Required | Day of the sitting |
| StartTime | TimeOnly / time | Required | Opening time |
| EndTime | TimeOnly / time | Required, after StartTime | Closing time |
| Capacity | int | 1-500 | Maximum guests |
| IsClosed | bool / bit | | Closed to new reservations |
| ClosedReason | string / nvarchar(20) | Enum: None, BookedOut, PrivateEvent | Why it is closed |

## Customers

| Column | Type | Constraints | Description |
|---|---|---|---|
| CustomerId | int | PK, identity | Customer key |
| FirstName | string / nvarchar(60) | Required | |
| LastName | string / nvarchar(60) | Required | |
| Email | string / nvarchar(256) | Required, indexed | |
| PhoneNumber | string / nvarchar(30) | Required | |
| UserId | string / nvarchar(450) | FK to AspNetUsers, nullable, unique | Set when the customer is a registered member (one-to-one) |

## Reservations

| Column | Type | Constraints | Description |
|---|---|---|---|
| ReservationId | int | PK, identity | Reservation key |
| SittingId | int | FK to Sittings, restrict delete | Sitting booked |
| CustomerId | int | FK to Customers, restrict delete | Guest |
| StartTime | TimeOnly / time | Within the sitting | When the guests arrive |
| DurationMinutes | int | 15-480 | Length of the booking |
| NumberOfGuests | int | 1-100 | Party size |
| Source | string / nvarchar(20) | Enum: Online, Mobile, InPerson, Email, Phone | How it was requested |
| Status | string / nvarchar(20) | Enum: Pending, Confirmed, Cancelled, Seated, Completed | Workflow state (changed only by `Confirm`, `Cancel`, `Seat`, `Complete`) |
| SpecialRequests | string / nvarchar(500) | Nullable | Notes such as "highchair required" |

## ReservationTables (join table)

| Column | Type | Constraints | Description |
|---|---|---|---|
| ReservationId | int | PK part 1, FK to Reservations, cascade delete | |
| TableId | int | PK part 2, FK to Tables, restrict delete | |

## ASP.NET Core Identity tables

`AspNetUsers` (login accounts, email as username), `AspNetRoles` (Manager, Staff, Member) and `AspNetUserRoles` are created by Identity. Passwords are stored only as hashes (`PasswordHash`).
