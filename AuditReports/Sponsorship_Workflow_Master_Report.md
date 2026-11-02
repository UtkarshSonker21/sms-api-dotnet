# Kafa’at SMS — Sponsorship Workflow Master Report

**Documentation status date:** 30 October 2026
**Authority:** This report is the current sponsorship-workflow source of truth. It consolidates the prior focused audits and plans listed below. Source-code statements describe the checked repository only; they do not prove deployed behavior or physical SQL Server schema.

## 1. Overall sponsorship workflow and business rules

The lifecycle visible in the prototype is broadly: student is awarded → sponsorship is assigned either to the DA Pool or a donor → costs/funding are administered → student proceeds through registration and academic study → graduation. The prototype demonstrates more screens than the SMS source currently implements end to end. In particular, donor allocation, receipts, disbursement and reconciliation remain simulated in the prototype and are not established as persisted SMS workflows.

Current SMS distinguishes application status from sponsorship assignment. `KfStudentProgramApplication.ApplicationStatus` is changed by the student-application status workflow; a DA Pool category assignment is a separate record and does not itself change the application status. Initial academic registration transitions a sponsored application to Registered. This report does not interpret DA Pool as a funding source or lifecycle state beyond what source establishes.

The source enum and transition audit identify `Awarded` (ID 5), `SponsoringInProcess` (ID 7), `Sponsored` (ID 8), and `SponsoringRejected` (ID 9). NGO status flow is `Awarded → SponsoringInProcess → Sponsored or SponsoringRejected`; `StudentProgramApplicationService.ChangeStatusAsync` records status, optional remarks, actor/time audit fields and a generic `KfStudentHistory` item. The client prototype instead offers direct Awarded-to-DA-Pool or Awarded-to-Donor-Sponsored actions, with both becoming its shared Sponsored status. These are different state machines; the category assignment API does not reconcile or replace either one.

Confirmed category-assignment rule: staff choose a student sponsorship category, and that category permits **all active sponsorship types mapped to that category** in `kf_sponsorship_category_mapping` (with the category and type themselves active). Staff do not select one type. The assignment is an administrative category choice and is **not verified student-category eligibility**: no authoritative `StudentCategoryId` relationship was found on the application or student-registration entities inspected. No schema change was authorized to add one.

Unresolved policy must remain unresolved in implementation: committee versus Marketing ownership of donor assignment; what DA Pool means operationally; donor/student/supervisor cardinality; payment and allocation structure; and how matrix eligibility, program cost estimates, donor commitments and actual payments relate.

## 2. Existing database tables and relationships

The following are source-model findings from entity classes and `AppDbContext`, not physical database verification:

| Source entity/table | Relevant responsibility and relationships |
|---|---|
| `KfSponsorshipType` / sponsorship-type table | Configured sponsorship type, including name, `FrequencyType`, activity and display attributes. Matrix and program-cost records reference it. |
| `KfSponsorshipStudentCategory` / `kf_sponsorship_student_categories` | Category lookup with activity/display fields. |
| `KfSponsorshipCategoryMapping` / `kf_sponsorship_category_mapping` | Category-to-type mapping, activity state and foreign keys to category/type. EF declares unique `(SponsorshipTypeId, StudentCategoryId)` (`UQ_kf_sponsorship_category_mapping`); this is not evidence that the deployed index exists. |
| `KfStudentSponsorshipAssignment` / `kf_student_sponsorship_assignments` | Assignment row links `ApplicationId` to one `SponsorshipCategoryMappingId`, with `IsActive`, `AssignedBy`, `AssignedDate`, nullable `UpdatedBy`, `UpdatedDate`, and `Remarks`. EF maps FKs to application, mapping and actor users, plus ordinary indexes on application and mapping. No EF unique active-assignment constraint is declared. |
| `KfStudentProgramApplication` | Application/status/history and related academic records; has sponsorship-assignment collection in the current model. It has no confirmed student-category field. |
| `KfStudentHistory` | General student/application history and actor relationship. Current category assignment service does not add a history record for assignment changes. |
| `KfProgramCost` | Program-level configured amount by sponsorship type, activity and audit data. Frequency comes from linked sponsorship type; it is not a student-specific charge, commitment or payment ledger. |
| `KfMarketingAdministrativeFee` (entity/service) | Marketing administrative-fee configuration/history. No source evidence connects it to a student financial ledger or payment allocation. |
| Academic records (`KfProgramSemester`, academic registration, course enrollment/results, semester results) | Separate academic lifecycle. These do not establish donor funding, payment or cost settlement. |

Legacy donor-related model classes such as `MasterDonorList` and `StudentReqList` were found in prior source inspection, but no current DbSet/model mapping or active donor-allocation service was established. Their existence does not establish a deployed table or usable donor workflow.

## 3. Sponsorship Matrix and category-to-sponsorship-type mapping

The Matrix is the existing source configuration for which types are permitted per category. In current API source, active options are filtered through active category mapping, category and sponsorship type. The assignment API derives permitted types from the Matrix when returning assignment data, rather than saving a frozen copy of all types. Therefore a later Matrix edit can change the currently returned permissions for an existing assignment; historical entitlement snapshots are not represented by this assignment contract.

Prior audit evidence shows category and sponsorship-type create/update/soft-deactivate/search/read operations and Matrix read/toggle are connected to Angular and API CRUD/configuration code. The Matrix reset action was identified as a TODO, rather than a complete server reset workflow. Sponsorship `FrequencyType` is a byte; Angular labels `1` Recurring and `2` One-time, but prior source inspection found no server-side range/enum validation, so those numeric values should not be treated as a guaranteed API enum contract until validated. Controllers use authenticated access patterns, but generic `[Authorize]` alone does not establish that only intended staff roles can edit Matrix/category/type configuration. EF uniqueness declarations also require physical-schema confirmation. Matrix toggle behavior and concurrent duplicate protection must be checked before operational reliance.

Program costs are distinct: `KfProgramCost` stores the program, sponsorship-type ID and amount; frequency is derived from `KfSponsorshipType.FrequencyType`. No source evidence establishes that category mapping determines a student's bill or that Matrix changes produce an individual student charge.

## 4. DA Pool category assignment workflow

### Current source behavior (30 October 2026 review)

1. NGO staff open the Student Details DA Pool assignment modal.
2. The UI requests category options / active permitted types and displays one category selector plus a read-only permitted-type list and optional remarks. The individual sponsorship-type selector has been removed.
3. The UI submits `StudentCategoryId` and optional `Remarks` to `POST api/ngo/student-sponsorship-assignments/applications/{applicationId}`. It does not send an authoritative set of allowed sponsorship types.
4. `StudentSponsorshipAssignmentService.CreateAsync` checks NGO access, validates application/category input, opens a serializable transaction, and revalidates that the selected category is active and has at least one active mapping to an active type.
5. Because the existing assignment table stores a single `SponsorshipCategoryMappingId`, the service stores one deterministic anchor mapping (lowest mapping ID) to represent the selected category. It derives all currently permitted active types from that category when reading the assignment.
6. The Student Details GET reloads the saved assignment/category and current permitted type list. Update and deactivate operations preserve audit fields and deactivate old rows rather than deleting them.

Relevant source: `ScholarshipManagementAPI/Services/Implementation/Ngo/StudentSponsorshipAssignmentService.cs`, `Controllers/Ngo/StudentSponsorshipAssignmentsController.cs`, `DTOs/Ngo/SponsorshipAssignments/SponsorshipAssignmentDtos.cs`, `Data/DbModels/KfStudentSponsorshipAssignment.cs`, `Data/Contexts/AppDbContext.cs`; Angular `src/app/features/ngo/ngo-student-detail/ngo-student-detail.ts`, `.html`, `.scss`, and shared `src/styles.scss`.

### Important limitations

- The anchor mapping is a compatibility technique, not a schema-native category assignment. It is important that consumers treat the returned category as authoritative for the logical assignment and not interpret the anchor's single type as the whole permission set.
- Distinct active categories for one application can coexist in current service logic. There is no source-established one-active-category-per-application rule. Do not impose uniqueness without business approval and physical duplicate-data review.
- Existing rows created under earlier one-mapping-per-type semantics are grouped by category on read where possible. Historical rows cannot always encode whether multiple same-category rows were one logical assignment or distinct actions.
- Category selection is not checked against verified student eligibility because the required student-category relationship is absent.
- Assignment creation/update/deactivation do not transition application status or write `KfStudentHistory`.
- The API controller is `[Authorize]`; service checks NGO user type/login. Resource-level scope (assigned case load, university, or global NGO access) was not established and needs security/business review.

The UI now uses the existing scoped `da-select` presentation pattern in `src/styles.scss` with category component classes and keyboard/focus behavior. Source and TypeScript checks are evidence of implementation, not browser-level visual/accessibility verification.

## 5. Donor-sponsored workflow and pending client clarifications

The prototype's Student Details dialog allows a user to mark a student Donor-Sponsored and enter donor name/reference as free text. That dialog is not proof of a donor master lookup, allocation, payment, or receipt workflow. Prototype donor directory, awaiting-allocation queue, allocation screens, reports and finance screens use localStorage/static JavaScript in the inspected prototype; they are simulations, not SMS persistence.

No complete current SMS donor directory/assignment/supervisor, donation receipt, allocation, disbursement, balance, unallocated-funds or reconciliation API/model flow was established in the reviewed source. Do not implement one from the prototype alone. The following decisions gate design:

| Client decision | What existing evidence suggests | Choices / implementation impact |
|---|---|---|
| Committee vs Marketing ownership of donor assignment | Committee Student Details simulates donor name/reference; Marketing prototype has allocation screens. SMS does not establish the hand-off. | Choose one owner or explicitly split nomination and allocation; controls endpoint roles and audit actor. |
| Meaning of DA Pool | Prototype distinguishes DA Pool from Donor-Sponsored; current SMS assignment stores category separately from application status. | Define whether DA Pool is a funding pool, temporary queue, or status; determines status transitions, eligibility and subsequent allocation. |
| Donor cardinality | No source-backed donor assignment relationship confirmed. | One donor per student, multiple donors, and/or donor per semester require different relationships and history. |
| Donation supervisors | Prototype suggests a supervisor association; SMS persistence not confirmed. | Single vs multiple supervisors determines mapping/cardinality and authorization. |
| Payment/receipt scope | Prototype simulates receipt and allocation screens. | Decide if one receipt covers multiple students/semesters/cost items, partial allocations and reversals; affects financial ledger design. |
| Matrix, program cost and payment relationship | SMS has category/type mappings and program-level cost estimates; no per-student billing/payment bridge confirmed. | Define eligibility versus budget estimate versus obligation versus cash allocation; dictates source-of-truth and historical snapshots. |
| Category eligibility | Current category is staff-selected; no authoritative student-category FK found. | Identify trusted category source or explicitly confirm free administrative selection. |

## 6. Backend API, service, entity and DTO status

**Status terminology:** “Implemented, source-verified” means code paths and mappings were inspected. It does not mean deployed DB/runtime behavior was tested.

| Capability | Status | Source evidence / limitation |
|---|---|---|
| Sponsorship type/category/Matrix configuration | **Partially implemented** | API and Angular Matrix/configuration flows exist in prior audit evidence. Role policy, physical indexes and deployment parity remain unverified. |
| Matrix reset and valid frequency enforcement | **Partially implemented / validation gap** | Reset was reported as TODO; `FrequencyType` is byte-backed and its allowed numeric values were not restricted server-side in the reviewed paths. Confirm the intended values and server-side role/validation contract. |
| DA Pool category option retrieval | **Implemented, source-verified** | `StudentSponsorshipAssignmentService.GetOptionsByCategoryAsync` filters active Matrix/category/type and requires NGO access. |
| Category-based assignment create/read/update/deactivate | **Implemented, source-verified** | Controller routes and service use `kf_student_sponsorship_assignments`; save revalidates category mappings; reads derive complete active type list; assignment history is retained as inactive rows. No runtime DB verification. |
| Student category eligibility verification | **Not implemented / dependency missing** | No authoritative category relationship on application/student registration confirmed. Category assignment must not be presented as eligibility validation. |
| Donor directory, donor allocation, supervisor mapping | **Missing from verified active API flow** | Legacy classes are not evidence of active mapped/API behavior. Client decisions required. |
| Receipt/payment/disbursement/reconciliation | **Missing from verified active API flow** | No persisted end-to-end financial ledger established. Prototype behavior is mock. |
| Student history of sponsorship decisions | **Partially implemented** | Generic `KfStudentHistory` exists and status changes create history; DA Pool assignment service itself does not create history. Student Details audit identified a UI/source gap between history display and persisted history endpoint; verify before relying on detail view. |
| Program cost configuration | **Implemented for program estimates** | Program service persists `KfProgramCost`; frequency derived from sponsorship type. No student charges/payment settlement integration. |
| Marketing Administrative Fee | **Partially implemented** | Configuration and history source exist; no verified application to per-student ledger/disbursement. |
| Registration/academic progression/graduation funding effects | **Missing/unconfirmed** | Academic registration/progression exists separately; no confirmed automatic financial charge or graduation settlement integration. |

The assignment contract is category-only: `CreateSponsorshipAssignmentRequestDto` contains `StudentCategoryId` and optional remarks. Response includes `StudentCategoryId` and `PermittedSponsorshipTypes`; legacy `SponsorshipCategoryMappingId` remains exposed as the anchor. No API contract should imply a committed financial allocation.

## 7. Angular UI implementation status

- Student Details has a category-only assignment modal, optional remarks and a derived list of all active permitted types. It reloads assignment data when reopening the detail workflow and supports update/deactivation through existing API service calls.
- Latest dropdown uses the application's `da-select` shared style rules in `src/styles.scss`; component-specific scope is in Student Details SCSS. TypeScript application compilation was reported successful on 30 October 2026.
- Browser visual comparison, keyboard interaction in a running browser and live save/reopen behavior were not performed in the evidence available for this report.
- Other sponsorship UI status from prior audit: Matrix/category/type configuration is connected in source, while prototype Marketing donor allocation, receipts and finance remain mock-only and have no confirmed live SMS counterpart.
- Do not treat the presence of prototype buttons or Angular screens as proof of API persistence.

## 8. Student Details and prototype parity findings

Prototype reference files include `kafaat-dashboard/en/committee/student-detail.html`, `committee/students.html`, `committee/approvals.html`, Matrix screens and assets including `semesters.js` and `failing-core.js`. Prototype interactions use localStorage/static data. Its profile includes journey/status indicators, mock academic/cost lines, paid flags, and donor name/reference. Those values are not authoritative SMS records.

The inspected prototype stores student examples under `kafaat.students.v2` and the Matrix under `kafaat.sponsorshipMatrix.v1`; categories can fall back to static mock values. Committee detail uses “Mark as Sponsored (DA pool)” and “Mark as Donor-Sponsored”; its modal requires a category and, for donor sponsorship, free-text donor name/reference. Marketing’s awaiting-donor modal additionally displays donor, supervisor, semesters, payment method and receipt reference, but submit only marks a student ID allocated/removes it from the local queue; the entered allocation details are not persisted. Receipt logging was identified with no effective handler; Finance lists and paid flags are seeded/local state. Do not interpret those controls or sample totals as a real workflow.

SMS Student Details is backed by application/student APIs for the supported fields. Prior Student Details audit found authorization/resource-scope concerns in detail/document/history reads, a current UI history display not consistently backed by persisted history, University remarks not persisted in the inspected path, and prototype finance fields without matching API data. These issues are not resolved by category assignment. Re-check current implementations before separately changing them.

More specifically, the existing persisted history read endpoint was found, but the NGO Student Details component synthesized at most one local “Status Updated” row from application status/action date instead of loading that history feed. The University remark action only changed the client object and showed success; it did not persist through a dedicated API contract. The NGO detail read was filtered by application status for NGO staff but had no confirmed per-caseload/university resource check; prior audit also identified missing default-deny branches for some authenticated staff types in detail/document/history paths. Confirm the intended policy and enforce it server-side before relying on these routes. The NGO student-list total came from server `totalCount`, but status KPIs/tabs were computed from the current page’s items (page size 25), so counts could be incomplete on later pages. University list used a 100,000-row request cap for its local counts, not a true aggregate.

The prototype's Marketing donor directory, “Awaiting Donor” queue, allocations, receipt logging and finance reports are not evidence that the SMS has those persisted capabilities. Prototype controls may calculate locally or have no persistence handler. Awarded, Sponsored/DA Pool, Donor-Sponsored, Registered and Graduated must remain distinct until the client defines transitions; SMS application status and category assignment are separate concepts.

## 9. Database changes applied versus pending

### Confirmed from repository

- Current EF entity/configuration expects `kf_student_sponsorship_assignments` with `AssignmentId`, `ApplicationId`, `SponsorshipCategoryMappingId`, `IsActive`, `AssignedBy`, `AssignedDate`, nullable updater fields and optional remarks (max 500 in EF).
- Current service relies on the existing single-mapping FK as a category anchor and derives all active permissions from Matrix mappings.
- `AppDbContext` declares non-unique indexes for assignment application and mapping IDs and foreign keys to application, mapping and user rows. Matrix mapping has an EF unique index on sponsorship type/category.
- No physical database connection, metadata query, migration, SQL execution or deployment is evidenced. Therefore physical table/column/index/FK existence, data compatibility and production status are **unverified**.

For a later authorized read-only database review, verify the application/student parent keys and FKs; category/type/mapping table columns, nullability and active flags; mapping unique pair index; assignment table columns, types, nullability, defaults, PK, FKs, delete actions and all indexes; any competing/legacy donor or assignment objects; plus aggregates for orphaned assignment references, duplicate `(ApplicationId, SponsorshipCategoryMappingId)` rows, multiple distinct active categories per application, inactive/missing Matrix references, program-cost rows with missing/inactive types, invalid frequency values and duplicate current Marketing Admin Fee rows. Use aggregate counts and non-sensitive IDs only. No such query was executed for this report.

### Provisional SQL is stale and must not be executed

Retain `DA_Pool_Sponsorship_Deployment_Provisional.sql` as a separate artifact, but mark it **provisional / incompatible pending reconciliation**. Its proposed create-table branch uses a direct `StudentCategoryId`, `CreatedBy`/`CreatedDate` and `RowVersion`, and proposes a filtered unique active-application index. The current entity/service instead requires `SponsorshipCategoryMappingId`, `AssignedBy`/`AssignedDate`, does not declare `RowVersion`, and allows distinct active categories to coexist. The SQL is not evidence that any database change was applied. Do not run it. Reconcile or replace it only after schema ownership and business cardinality are approved.

The prior database inventory included read-only catalog checks and a historical schema proposal. The verification checklist above carries forward the useful checks; the old proposal is superseded by current source and the incompatibility described above. Any deployment must first verify intended server/database identity, actual objects, existing rows, FKs/indexes and duplicate semantics; do not infer physical state from EF.

## 10. Testing and build verification

Evidence available as of 30 October 2026:

| Check | Result | Boundary |
|---|---|---|
| API `dotnet build --no-restore` after category-assignment implementation | **Passed**; prior full build reported two nullable warnings in unrelated `MarketingAdministrativeFeeService.cs:127` and `ProgramRegistrationWindowService.cs:37`; later incremental build reported zero warnings. | Build evidence only; no endpoint/runtime/DB check. Not rerun for this documentation task. |
| Angular `npx tsc --noEmit -p tsconfig.app.json` | **Passed** | Compiler only; no browser verification. |
| Angular `npm run build` | **Blocked/failed** with `spawn EPERM` | Environment/process launch issue was reported; no successful production build established. |
| Focused assignment/API tests | **Not run / no relevant test project identified in prior evidence** | Need isolated fixtures/test doubles or approved non-production integration environment. |
| Browser category dropdown/save/reopen | **Not tested** | Requires browser session and suitable authorized fixture. |
| Database schema/data | **Not verified** | Requires authorized read-only metadata and data checks against a confirmed non-production target. |
| SQL/migration/API mutation | **Not performed** | No database writes or deployment. |

Recommended acceptance coverage: active category with multiple active types; inactive category/mapping/type rejected; no mapping rejected; category save and reopen shows all current permitted types; optional remarks; same category retry idempotency; changing category deactivates old rows and preserves history; multiple active categories behavior per policy; unauthorized/non-NGO access; concurrent create/update/deactivate; matrix changes reflect current permissions; no application-status/history/financial mutation; verify failed request leaves data unchanged.

Add lifecycle/UI regression coverage for Awarded → SponsoringInProcess → approve/reject transitions; direct status bypass and repeated/concurrent status updates; Student Details persisted history and remark behavior; NGO resource-scope and non-NGO default-deny; paged KPI counts; Matrix reset, invalid frequency and inactive parent IDs; and preserve old program-cost interpretation when a sponsorship type frequency changes according to the approved historical policy. Finance acceptance tests should distinguish incoming receipt from outgoing disbursement, allow/disallow one receipt across multiple students/semesters per decision, and cover partial allocations, duplicate references, refunds/reversals, concurrency, currency, rounding, balance and reconciliation. These remain planned tests, not executed evidence.

## 11. Remaining implementation tasks and dependencies

### P0 — before operational testing

1. Confirm the target environment is non-production and verify database identity without exposing credentials.
2. Have an authorized DBA compare physical schema to current entity configuration. Specifically reconcile the assignment table with the retained provisional SQL; do not execute that artifact.
3. Confirm NGO role and resource-level access policy for Student Details and assignment endpoints.
4. Decide whether one or multiple active categories are allowed per application, and whether category is eligibility evidence or an administrative assignment only.

### P1 — sponsorship workflow requirements

1. Agree how category assignments are historically represented and whether current Matrix edits change existing permitted types or should preserve snapshots.
2. Connect Student Details history to persisted sponsorship/status events if the client expects an auditable journey; decide whether category assignment creates a history event and what event payload is required.
3. Define donor ownership, donor model, assignment cardinality and supervisor relationship before adding donor operations.
4. Define receipt/payment allocation, partial funding, reversals, currency, cost items, balances and reconciliation before finance persistence.

### P2 — integration and reporting

1. Integrate approved financial ledgers with program cost estimates only after accounting semantics are settled.
2. Define whether registration/progression creates recurring charges and when one-time fees apply.
3. Replace prototype/mock-only reporting with API-backed reporting after source-of-truth and authorization are defined.
4. Establish automated, isolated tests and browser acceptance checks; resolve Angular `spawn EPERM` in an approved build environment without altering dependencies merely to hide it.

## 12. Deployment and rollback considerations

- No database change is confirmed applied. Do not deploy the provisional SQL unchanged.
- Before any deployment, compare current database catalog and row data with the source model in an approved environment, check for existing assignment rows and duplicate semantics, and obtain explicit review of the exact deployment artifact.
- The current category-anchor design avoids a new column/table but depends on a valid active mapping row. If the selected category loses all active mappings, new assignment is rejected; existing assignments can return an empty current permission list. Matrix edits are live, not historically snapshotted.
- If later schema design changes assignment shape/cardinality, plan compatibility for existing `SponsorshipCategoryMappingId` records and preserve assignment IDs/audit fields. Define a backup/restore and transactionally controlled forward/rollback procedure with the DBA. No rollback script has been verified as safe for production data.
- Never roll back by deleting assignment history or altering student/application status without an approved data-preservation plan.

## Consolidation provenance and authority

This report consolidates the sponsorship findings, risks, checklists, database verification requirements and plans from all six prior Markdown reports named in the consolidation task. It is the single sponsorship workflow Markdown source of truth in `AuditReports`. The separate `DA_Pool_Sponsorship_Deployment_Provisional.sql` file is retained unchanged and is explicitly not approved for execution. Broader academic lifecycle findings are included only where relevant to sponsorship.

## Final assessment

**DA Pool category assignment is implemented in source and passed the reported API build and Angular TypeScript check, but is not verified against a deployed database or at runtime.** The shared dropdown is implemented in source, with browser verification outstanding. Sponsorship as a full donor-to-finance lifecycle is **not ready for end-to-end implementation testing** until database compatibility, role/resource authorization, category semantics and donor/finance decisions are resolved. The first safe action is a read-only schema/environment verification and security/business sign-off; no apply or financial operation should be attempted beforehand.
