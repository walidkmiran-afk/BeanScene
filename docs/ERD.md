# Entity relationship diagram and class diagram

## ERD (Mermaid)

Paste the code block into https://mermaid.live or open this file in VS Code / GitHub to see the diagram.

```mermaid
erDiagram
    AREA ||--o{ TABLE : "has many"
    SITTING ||--o{ RESERVATION : "has many"
    CUSTOMER ||--o{ RESERVATION : "makes many"
    RESERVATION ||--o{ RESERVATION_TABLE : "holds"
    TABLE ||--o{ RESERVATION_TABLE : "used by"
    ASPNETUSERS |o--o| CUSTOMER : "member account (one-to-one)"
    ASPNETUSERS }o--o{ ASPNETROLES : "AspNetUserRoles (one role each in practice)"

    AREA {
        int AreaId PK
        string AreaName UK
    }
    TABLE {
        int TableId PK
        string TableName UK
        int AreaId FK
        int Capacity
    }
    SITTING {
        int SittingId PK
        string SittingType
        string Name
        date SittingDate
        time StartTime
        time EndTime
        int Capacity
        bool IsClosed
        string ClosedReason
    }
    CUSTOMER {
        int CustomerId PK
        string FirstName
        string LastName
        string Email
        string PhoneNumber
        string UserId FK "nullable, unique"
    }
    RESERVATION {
        int ReservationId PK
        int SittingId FK
        int CustomerId FK
        time StartTime
        int DurationMinutes
        int NumberOfGuests
        string Source
        string Status
        string SpecialRequests
    }
    RESERVATION_TABLE {
        int ReservationId PK,FK
        int TableId PK,FK
    }
```

Every Bean Scene table also has `CreatedAt`, `UpdatedAt` and `RowVersion` (from `BaseEntity`).

## Relationships

| Relationship | Type | How it is mapped |
|---|---|---|
| Area - Table | One-to-many | `Table.AreaId` FK, `Area.Tables` / `Table.Area` navigation properties |
| Sitting - Reservation | One-to-many | `Reservation.SittingId` FK |
| Customer - Reservation | One-to-many | `Reservation.CustomerId` FK |
| Reservation - Table | **Many-to-many** | `ReservationTable` join entity with composite key (`ReservationId`, `TableId`) |
| ApplicationUser - Customer | **One-to-one** (optional) | `Customer.UserId` FK with a unique index |
| ApplicationUser - ApplicationRole | Many-to-many (Identity) | `AspNetUserRoles`; the app gives each user exactly one role |

## Reservation status workflow

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> Confirmed : Confirm
    Confirmed --> Pending : Undo
    Pending --> Seated : Seat (needs a table)
    Confirmed --> Seated : Seat (needs a table)
    Seated --> Confirmed : Undo
    Seated --> Completed : Complete
    Completed --> Seated : Undo
    Pending --> Cancelled : Cancel
    Confirmed --> Cancelled : Cancel
```

Undo is not allowed for a day that has passed. A cancelled booking cannot be brought back.

## Class diagram (Mermaid)

```mermaid
classDiagram
    class IAuditable {
        <<interface>>
        +DateTime CreatedAt
        +DateTime UpdatedAt
    }
    class BaseEntity {
        <<abstract>>
        +DateTime CreatedAt
        +DateTime UpdatedAt
        +Guid RowVersion
    }
    IAuditable <|.. BaseEntity
    BaseEntity <|-- Reservation
    BaseEntity <|-- ReservationTable
    BaseEntity <|-- Sitting
    BaseEntity <|-- Table
    BaseEntity <|-- Area
    BaseEntity <|-- Customer

    class Reservation {
        +int ReservationId
        +ReservationStatus Status
        +Confirm()
        +Cancel()
        +Seat()
        +Complete()
        +Undo()
    }
    Sitting "1" --> "*" Reservation
    Customer "1" --> "*" Reservation
    Reservation "1" --> "*" ReservationTable
    Table "1" --> "*" ReservationTable
    Area "1" --> "*" Table
    ApplicationUser "1" --> "0..1" Customer
    IdentityUser <|-- ApplicationUser

    class IReport {
        <<interface>>
        +Generate(reservations) ReportResult
    }
    class ReportBase {
        <<abstract>>
        #GroupKey(reservation) string
    }
    IReport <|.. ReportBase
    ReportBase <|-- StatusSummaryReport
    ReportBase <|-- SourceSummaryReport
    ReportBase <|-- SittingSummaryReport

    class DoublyLinkedList~T~ {
        -Node head
        -Node tail
        +AddFirst(T)
        +AddLast(T)
        +Remove(T) bool
    }
    class PendingFollowUpQueue
    PendingFollowUpQueue o-- DoublyLinkedList~T~
```