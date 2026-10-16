# Walkthrough: what is in the project and how to defend it

This maps the assessment (Parts 2 to 5) to the code. Read it once, open each file listed, and make sure you can explain it in your own words. The README explains how to build and run the app.

## 1. The shape of the solution

| Layer | Folder (BeanScene.Web) | Job |
|---|---|---|
| Models | `Models/` | Entities (Area, Table, Sitting, Customer, Reservation, ReservationTable), enums, Identity user/role, view models |
| Data | `Data/` | `ApplicationDbContext` (EF Core mapping), `DbSeeder` (roles, users, tables, sittings) |
| Services | `Services/` | Business rules and all database work (`ReservationService`, `SittingService`, `ReportService`, `OrmDemoService`). Controllers call these |
| Controllers | `Controllers/` | MVC controllers plus the REST API in `ApiControllers.cs` |
| Views | `Views/` | Razor pages with Bootstrap. Shared pieces are in `Views/Shared` |
| Scripts | `wwwroot/js/` | Plain JavaScript that updates the page in place with `fetch` |
| OOP extras | `DataStructures/`, `Reports/` | Doubly linked list, sorting and searching, report classes |
| Tests | `BeanScene.Tests/` | xUnit tests on in-memory SQLite |

Request flow: browser, controller, service (validation, transaction), DbContext, SQL Server, back up to a view.

## 2. Mapping to the assessment

### Part 2: OOP and data structures
- **Nested class / generics / IEnumerable:** `DataStructures/DoublyLinkedList.cs` is `DoublyLinkedList<T> : IEnumerable<T>` with a nested `Node` class.
- **Encapsulation:** `Models/Reservation.cs`. `Status` has a private setter and only changes through `Confirm()`, `Seat()`, `Complete()`, `Cancel()` and `Undo()`. Each one throws `InvalidOperationException` on an illegal move (for example Cancelled to Seated). `Undo()` goes one step back: Confirmed to Pending, Seated to Confirmed, Completed to Seated.
- **Interfaces and polymorphism:** `Reports/Reports.cs`. `IReport`, abstract `ReportBase` (template method), and three subclasses. `ReportService` runs them all through the interface.
- **Sorting:** `DataStructures/Algorithms.cs`, `SortingAlgorithms.MergeSort` and `QuickSort`, written by hand.
- **Searching:** `SearchingAlgorithms.LinearSearch` and `BinarySearch` (binary search needs sorted input).
- **Used in the real app:** `PendingFollowUpQueue` takes the open bookings, merge-sorts them by sitting date and start time, and stores them in the doubly linked list. This is the order of the **Follow-up** page. It also keeps a second list, quick-sorted by last name, so a booking can be found by last name with binary search (`FindByLastName`).
- Be ready to state complexity: merge sort O(n log n); quick sort O(n log n) average, O(n^2) worst; binary search O(log n); linear search O(n); linked-list insert at ends O(1), search O(n).

### Part 3: ORM and database
- **Mapping and relationships:** `Data/ApplicationDbContext.cs`. One-to-many (Area to Table, Sitting to Reservation, Customer to Reservation), many-to-many (Reservation and Table through `ReservationTable`), one-to-one (`ApplicationUser` to `Customer`). Diagrams are in `docs/ERD.md` and every column is in `docs/DATA_DICTIONARY.md`.
- **Audit fields and concurrency:** `BaseEntity` has `CreatedAt`, `UpdatedAt` and a `[ConcurrencyCheck] Guid RowVersion`. The context overrides `SaveChanges` to stamp them (`StampEntities`).
- **Transactions:** `ReservationService.CreateAsync` and `AssignTablesAsync` use `BeginTransactionAsync`, then commit, or roll back on error.
- **Optimistic concurrency:** `UpdateAsync` sets the `RowVersion` original value from the form and catches `DbUpdateConcurrencyException`. Edit a reservation in two browser tabs and save both to see it.
- **Eager / lazy / explicit loading, entity states, exception handling:** `Services/OrmDemoService.cs` runs labelled experiments and the Manager page **ORM demo** shows the output. This is your evidence for these items.
- **Seeding and migrations:** `DbSeeder.cs` (roles, 30 tables, demo users, 90 days of sittings, topped up on every start) and the migrations, which the app applies when it starts.

### Part 4: security and the web app
- **Identity:** `Program.cs` (`AddIdentity`, password rules, lockout, unique email). Members log in with their email. A staff login is a username plus `@beanscene.test`.
- **Roles:** `Manager`, `Staff`, `Member` (`Models/Enums.cs`, `RoleNames`). Enforced by `[Authorize(Roles = ...)]` on controllers:
  - Manager only: Sittings, Reports, Accounts, ORM demo, delete reservation
  - Staff or Manager: Reservations (including Follow-up), Allocation, the REST API
  - Member only: My reservations (a member only ever sees their own, filtered by their linked Customer)
  - Anonymous: Home, Booking, Account
- **Anti-forgery:** global `AutoValidateAntiforgeryTokenAttribute` in `Program.cs`; forms get the token automatically.
- **Validation:** data annotations on view models, plus business rules in the services (capacity, closed sittings, time window, table overlap, past days). The server is the real protection. The page only makes mistakes harder to make.
- **REST API:** `Controllers/ApiControllers.cs`, route `api/reservations` with GET, GET by id, POST, PUT, PUT status, DELETE (Manager). Cookie login at `api/auth/login`. Unauthorised API calls return 401/403 instead of redirecting to the login page. Use `docs/api-requests.http`.

### The design approach (how the pages work)
- **In-place updates.** Editing, confirming, assigning tables, saving a day, adding a booking and managing accounts all happen on the same page. `wwwroot/js/booking-actions.js` sends each form with `fetch` and refreshes only the part of the page that changed.
- **One shared booking panel.** `Views/Shared/_BookingActions.cshtml` is used by the Sittings, Reservations, Follow-up and Accounts pages, so Edit, Tables, Confirm, Seat, Complete, Cancel and Delete look and behave the same everywhere.
- **Seat and tables.** Tick a table and press Seat. The tables are saved and the guests seated in one step. If no table is ticked, Seat opens the Tables panel instead. The server also refuses to seat a booking with no table.
- **Undo.** Once a booking is Confirmed, Seated or Completed, the same button becomes Unconfirm, Unseat or Uncomplete and goes one step back. It is not allowed on a day that has passed.
- **Past days.** A booking on a day that has passed shows a grey Past tag and its buttons are disabled. This is worked out from the date each time and is never stored. In the manager's calendar, a day that has passed is greyed out and read-only, and the server refuses changes to it.
- **Time boxes.** The time and seats boxes in the weekly hours and the day editor share one set of rules in `wwwroot/js/time-boxes.js`. A box that can't be understood turns red straight away, and nothing saves until it's fixed.
- **Accounts.** A member's row on the Accounts page has Bookings and Book buttons that drop open the member's reservations and a booking form, under the row.

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

Other automated tests: capacity, closed sitting, outside time window, seat requires tables (`ChangeStatus_SeatWithoutTables_IsRejected_ThenAllowedOnceTablesAreAssigned`), domain state machine, linked list and algorithms, and the status report. The two tests that book two guests at the same time use different phone numbers on purpose, because one phone number cannot hold two overlapping bookings.
Use whatever 8 suit your plan's test table, but keep the wording of your document and these cases consistent.

## 4. Debugging evidence

The brief usually asks you to show debugging. Do this once and screenshot each step:
1. Breakpoint on the first line of `ReservationsController.Create` (POST). Submit the form. Show the model bound into `input` in the Locals window.
2. Step into `ReservationService.CreateAsync`; breakpoint at `ValidateSlotAsync`. Show a failing case (over capacity) returning `OperationResult.Fail`.
3. Show the SQL: the console already logs EF commands (`appsettings.json`, level Information).
4. Browser DevTools Network tab: show the POST request and the response; for the API, the JSON response.

## 5. Final checklist

- Build the solution, run all tests (Test Explorer, Run All) and screenshot the result.
- Click through every flow once as Manager, Staff and Member.
- Take screenshots of the running app and save them in `docs/screenshots/` so they can go in the README.
- Commit to git regularly. The brief expects commit history over time, not one giant commit at the end.
- Register one account with your own real email to show the email requirement.
- Check the plan still matches what you built (page names, test cases).

## 6. Likely questions and short answers

- **Why services instead of putting logic in controllers?** Controllers stay thin; rules are in one place, unit-testable without a web server.
- **Why does the server check rules the page already checks?** Anyone can send a request without using the page, so the page is never the real protection.
- **Why an explicit `ReservationTable` class?** Many-to-many with a composite key, and I can query and control it directly. It also keeps room for extra columns later.
- **Why a Guid RowVersion rather than SQL Server `rowversion`?** It works the same on SQL Server and SQLite so the tests use the same logic. Trade-off: the app must regenerate it on save, which the context does.
- **Why a private setter on Status?** So no code can set an illegal status; transitions are checked in one place, including Undo.
- **Why is "Past" not stored?** It is worked out from the sitting date each time. A stored flag could go wrong or be out of date.
- **Why can a cancelled booking not be brought back?** Its time and tables are free for someone else. A new booking has to be made.
- **Why Restrict delete?** So deleting a sitting or area cannot silently wipe reservations.
- **What does `UseLazyLoadingProxies` need?** The Proxies package and `virtual` navigation properties. Lists use `Include` and `AsNoTracking` to avoid N+1 queries.
- **Why merge sort for the follow-up list?** Stable and guaranteed O(n log n); the list then stays in sitting order. Binary search on last name needs its own sorted list.
- **How is a double booking prevented?** Capacity check per sitting, table-overlap check on allocation, and the same phone or email cannot hold two overlapping bookings, all inside a transaction.
- **Why do pages update without reloading?** Staff enter many bookings in a row and should not lose their place. Customers are slowed down on purpose and are taken to a confirmation page.
- **Security weakness you know about?** Seed password is in `appsettings.json` for demo only; production would use user secrets or Key Vault. No email confirmation or password reset flow.
- **How would you test the JavaScript?** I tested it by hand. Next I would add automated tests with a tool like Jest for the time-box rules.