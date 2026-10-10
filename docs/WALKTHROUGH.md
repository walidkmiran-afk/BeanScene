# Walkthrough: what is in the project and how to defend it

This maps the assessment (Parts 2 to 5) to the code. Read it once, open each file listed, and make sure you can explain it in your own words.
**Nothing here was compiled when written, so build and run it first (see README.md).**

## 1. The shape of the solution

| Layer | Folder (BeanScene.Web) | Job |
|---|---|---|
| Models | `Models/` | Entities (Area, Table, Sitting, Customer, Reservation, ReservationTable), enums, Identity user/role, view models |
| Data | `Data/` | `ApplicationDbContext` (EF Core mapping), `DbSeeder` (roles, users, tables, sittings) |
| Services | `Services/` | Business rules and all database work. Controllers call these |
| Controllers | `Controllers/` | MVC controllers plus the REST API in `ApiControllers.cs` |
| Views | `Views/` | Razor pages with Bootstrap |
| OOP extras | `DataStructures/`, `Reports/` | Doubly linked list, sorting/searching, report classes |
| Tests | `BeanScene.Tests/` | xUnit tests on in-memory SQLite |

Request flow: browser, controller, service (validation, transaction), DbContext, SQL Server, back up to a view.

## 2. Mapping to the assessment

### Part 2: OOP and data structures
- **Nested class / generics / IEnumerable:** `DataStructures/DoublyLinkedList.cs` is `DoublyLinkedList<T> : IEnumerable<T>` with a nested `Node` class.
- **Encapsulation:** `Models/Reservation.cs`. `Status` has a private setter and only changes through `Confirm()`, `Seat()`, `Complete()`, `Cancel()`, which throw `InvalidOperationException` on illegal moves (for example Cancelled to Seated).
- **Interfaces and polymorphism:** `Reports/Reports.cs`. `IReport`, abstract `ReportBase` (template method), and three subclasses. `ReportService` runs them all through the interface.
- **Sorting:** `DataStructures/Algorithms.cs`, `SortingAlgorithms.MergeSort` and `QuickSort`, written by hand.
- **Searching:** `SearchingAlgorithms.LinearSearch` and `BinarySearch` (binary search needs sorted input).
- **Used in the real app:** `PendingFollowUpQueue` takes pending reservations, merge-sorts them by last name, stores them in the linked list, and binary-searches by last name. It powers **Reservations > Follow up**.
- Be ready to state complexity: merge sort O(n log n); quick sort O(n log n) average, O(n^2) worst; binary search O(log n); linear search O(n); linked-list insert at ends O(1), search O(n).

### Part 3: ORM and database
- **Mapping and relationships:** `Data/ApplicationDbContext.cs`. One-to-many (Area to Table, Sitting to Reservation, Customer to Reservation), many-to-many (Reservation and Table through `ReservationTable`), one-to-one (`ApplicationUser` to `Customer`). Diagrams are in `docs/ERD.md`.
- **Audit fields and concurrency:** `BaseEntity` has `CreatedAt`, `UpdatedAt` and a `[ConcurrencyCheck] Guid RowVersion`. The context overrides `SaveChanges` to stamp them (`StampEntities`).
- **Transactions:** `ReservationService.CreateAsync` and `AssignTablesAsync` use `BeginTransactionAsync`, then commit, or roll back on error.
- **Optimistic concurrency:** `UpdateAsync` sets the `RowVersion` original value from the form and catches `DbUpdateConcurrencyException`. Edit a reservation in two browser tabs and save both to see it.
- **Eager / lazy / explicit loading, entity states, exception handling:** `Services/OrmDemoService.cs` runs 8 labelled experiments and the Manager page **ORM demo** shows the output. This is your evidence for these items.
- **Seeding and migrations:** `DbSeeder.cs` and the migration you add (README step 3).

### Part 4: security and the web app
- **Identity:** `Program.cs` (`AddIdentity`, password rules, lockout, unique email). Email is the username.
- **Roles:** `Manager`, `Staff`, `Member` (`Models/Enums.cs`, `RoleNames`). Enforced by `[Authorize(Roles = ...)]` on controllers:
  - Manager only: Sittings, Reports, ORM demo, delete reservation
  - Staff or Manager: Reservations, Allocation, the REST API
  - Member only: My reservations (a member only ever sees their own, filtered by their linked Customer)
  - Anonymous: Home, Booking, Account
- **Anti-forgery:** global `AutoValidateAntiforgeryTokenAttribute` in `Program.cs`; forms get the token automatically.
- **Validation:** data annotations on view models, `SittingFormViewModel` implements `IValidatableObject` (end after start), plus business rules in the services (capacity, closed sittings, time window, table overlap).
- **REST API:** `Controllers/ApiControllers.cs`, route `api/reservations` with GET, GET by id, POST, PUT, PUT status, DELETE (Manager). Cookie login at `api/auth/login`. Unauthorised API calls return 401/403 instead of redirecting to the login page. Use `docs/api-requests.http`.

## 3. The eight test cases

Your plan lists test types. Suggested set, with the automated test that covers it where one exists:

| # | Type | Case | Automated test / how to show it |
|---|---|---|---|
| 1 | Functional | Create a valid reservation (ORM create and retrieve) | `Create_ValidInput_SavesPendingReservationAndCustomer` |
| 2 | Functional | Eager loading returns reservation with customer and sitting | `EagerLoading_IncludeLoadsRelatedDataInTheSameQuery` (and lazy: `LazyLoading_LoadsNavigationPropertyOnFirstAccess`) |
| 3 | Database | Transaction rollback leaves nothing saved | `AssignTables_WithUnknownTable_RollsBackAndSavesNothing`; commit case `AssignTables_Valid_PersistsManyToManyLinks` |
| 4 | Database | Concurrent edit is detected | `TwoContexts_EditingTheSameRow_RaiseDbUpdateConcurrencyException`, `Update_WithStaleRowVersion_IsRejectedAsConcurrencyConflict` |
| 5 | Security | Staff cannot open /Sittings or /Reports | Manual: log in as staff, visit `/Sittings`, expect Access Denied. Screenshot it |
| 6 | Security | Member sees only their own reservations | Manual: log in as member, open My reservations; staff-created ones for other people are absent |
| 7 | Interface | Booking form validation | Manual: submit empty form, bad email, 0 guests, see messages |
| 8 | Interface | Allocation screen disables tables already used at that time | Manual: allocate table M1 to one reservation, open another overlapping one, M1 is greyed out. Automated rule: `AssignTables_TableUsedByOverlappingReservation_IsRejected` |

Other automated tests: capacity, closed sitting, outside time window, seat requires tables, domain state machine (`ReservationDomainTests`), linked list and algorithms (`DataStructureTests`), status report.
Use whatever 8 suit your plan's test table, but keep the wording of your document and these cases consistent.

## 4. Debugging evidence

Brief usually asks you to show debugging. Do this once and screenshot each step:
1. Breakpoint on the first line of `ReservationsController.Create` (POST). Submit the form. Show the model bound into `input` in the Locals window.
2. Step into `ReservationService.CreateAsync`; breakpoint at `ValidateSlotAsync`. Show a failing case (over capacity) returning `OperationResult.Fail`.
3. Show the SQL: the console already logs EF commands (`appsettings.json`, level Information).
4. Browser DevTools Network tab: show the POST request and the 302 redirect response; for the API, the JSON response.

## 5. What you still have to do yourself

I cannot do these from here:
- Build, fix any compile error, add the migration (`Add-Migration InitialCreate`), run it.
- Run the tests and screenshot Test Explorer.
- Take screenshots and screen recordings of the running app and the demo.
- Commit to git regularly. The brief expects commit history over time. Do not make one giant commit at the end.
- Register one account with your own real email to show the email requirement.
- Update the plan so it matches what you built if anything differs (page names, test cases).

## 6. Likely questions and short answers

- **Why services instead of putting logic in controllers?** Controllers stay thin; rules are in one place, unit-testable without a web server.
- **Why an explicit `ReservationTable` class?** Many-to-many with a composite key, and I can query and control it directly. It also keeps room for extra columns later.
- **Why a Guid RowVersion rather than SQL Server `rowversion`?** It works the same on SQL Server and SQLite so the tests use the same logic. Trade-off: the app must regenerate it on save, which the context does.
- **Why a private setter on Status?** So no code can set an illegal status; transitions are checked in one place.
- **Why Restrict delete?** So deleting a sitting or area cannot silently wipe reservations.
- **What does `UseLazyLoadingProxies` need?** The Proxies package and `virtual` navigation properties. Lists use `Include` and `AsNoTracking` to avoid N+1 queries.
- **Why merge sort for the follow-up list?** Stable and guaranteed O(n log n); binary search then needs the sorted order.
- **How is a double booking prevented?** Capacity check per sitting, table-overlap check on allocation, both inside a transaction.
- **Security weakness you know about?** Seed password is in `appsettings.json` for demo only; production would use user secrets or Key Vault. No email confirmation or password reset flow.
