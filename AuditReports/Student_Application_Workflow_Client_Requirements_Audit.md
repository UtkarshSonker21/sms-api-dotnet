# Kafa’at SMS Student Application Workflow Client Requirements Audit

**Audit date:** 2 November 2026

## 1. Executive summary

**Not ready for final client-requirements end-to-end testing.** Source code implements the principal status transitions from application submission through Direct Aid Committee sponsorship review, but two explicit client requirements are absent: rejected candidates are not server-side blocked from applying again, and no University final-awarding-letter upload exists after a student becomes Sponsored. The client document's per-status viewer marks are unreadable/ambiguous, so exact role visibility cannot be verified. Source review is not runtime verification.

## 2. Scope and source documents

This audit compares the current Angular and ASP.NET Core student application workflow with the client document `Acceptance status.docx` only. The DOCX is the requirements authority. The HTML prototype was not used to define requirements. This is a source-level audit: no database connection, SQL, API request, browser interaction, migration, deployment, or application-source edit was performed.

The DOCX names three status families: Acceptance, Awarding, and Sponsoring. It identifies the University as the acceptance and awarding decision-maker, and the Direct Aid Committee as the sponsoring decision-maker. Its tables contain Viewer columns, but the extracted table cells have no readable marks showing which viewer can see each individual status. Those visibility details are recorded as **Not specified in client document**, rather than inferred from a column heading.

Developer-reported manual tests are recorded separately in section 7. They are not independent runtime verification.

## 3. Client requirements extracted from the Acceptance Status DOCX

| ID | Client requirement | Client terminology / role |
|---|---|---|
| ACC-01 | Accepted means the student successfully completed the documents needed for University approval. | Accepted; University determines status. |
| ACC-02 | In process means the student has not completed document submission and still has an opportunity to apply. | In process; University determines status. |
| ACC-03 | Not accepted means the candidate does not meet University requirements for the Kafa’at scholarship program. | Not accepted; University determines status. |
| ACC-04 | The DOCX presents School Coordinator and Direct Aid Committee under Acceptance “Viewed by.” It does not provide readable per-status viewer marks. | Viewer scope not specified per status. |
| AWR-01 | Awarded means the candidate received the final awarding-letter requirements from the University, including government approval of certificate equivalency. | Awarded; University determines status. |
| AWR-02 | Awarding in process means an accepted candidate has not yet satisfied the requirements for award or rejection from government or the University admission department. | In process; University determines status. |
| AWR-03 | Rejected means the student received rejection from the University or government certificate-equivalency body, and must be blocked from applying again. | Rejected; University determines status; reapplication blocked. |
| AWR-04 | The DOCX presents School Coordinator, Direct Aid Committee and Marketing Department under Awarding “Viewed by.” It does not provide readable per-status viewer marks. | Viewer scope not specified per status. |
| SPN-01 | Sponsored means an awarded student received Direct Aid Committee approval for a full-scholarship financial sponsorship. | Sponsored; Direct Aid Committee determines status. |
| SPN-02 | Sponsoring in process means the student has University awarding approval but has not yet received Direct Aid financial-sponsorship approval. | In process; Direct Aid Committee determines status. |
| SPN-03 | Sponsoring rejected means the Direct Aid Committee rejected financial sponsorship; the student must be blocked from applying again. | Rejected; Direct Aid Committee determines status; reapplication blocked. |
| SPN-04 | After Committee approval of sponsorship, the University must be able to send the final awarding letter to the student by uploading it in the system. | University action after Sponsored. |
| SPN-05 | The DOCX presents School Coordinator, University and Marketing Department under Sponsoring “Viewed by.” It does not provide readable per-status viewer marks. | Viewer scope not specified per status. |

The DOCX does not specify DA Pool category assignment, Sponsorship Matrix behavior, donor/payment work, semester registration, course enrollment, registration windows, exact rejection-reason fields, or status reversal. They are excluded from this audit.

## 4. Requirements-to-implementation matrix

| ID | Frontend evidence | Backend and persistence evidence | Result | Gap / test case |
|---|---|---|---|---|
| ACC-01 | `features/school/students` submits a document-complete application; University detail page exposes acceptance/awarding actions. | `StudentProgramApplicationService.SubmitApplicationAsync` checks `IsRequired` documents before `AcceptanceInProcess`; `ChangeStatusAsync` permits University `AcceptanceInProcess → Accepted`. `KfStudentProgramApplication.ApplicationStatus` persists the status. | Partially Implemented | Source supports document check then University acceptance. Runtime document/status round trip remains unverified. Test complete documents, accept, refresh application. |
| ACC-02 | School UI supports draft documents and Submit Application. | Documents can be uploaded while application is Draft; `SubmitApplicationAsync` rejects missing required documents and then changes to `AcceptanceInProcess`. | Inconsistent | Client says “In process” while documents remain incomplete; source uses Draft for that period and only enters AcceptanceInProcess after required documents are complete. Confirm intended terminology/workflow. |
| ACC-03 | University detail UI has rejection action for acceptance-stage workflow. | `ChangeStatusAsync` permits University `AcceptanceInProcess → AcceptanceRejected`; generic history is added. It does not establish a specific University-requirements reason model. | Partially Implemented | Status exists but DOCX term is “Not accepted,” source is `AcceptanceRejected`; reason semantics and runtime behavior unverified. Test rejection and reopen/history. |
| ACC-04 | School and NGO pages can load application data in source. | `GetByIdAsync`, search and status service contain role-dependent filters. | Not Verifiable | DOCX viewer marks are unreadable; current source does not prove exact intended viewer matrix or runtime authorization policy. |
| AWR-01 | `university-student-detail` presents awarding-review workflow actions. | `ChangeStatusAsync` permits University `AwardingInProcess → Awarded`; `ApplicationStatus` and generic history persist. | Partially Implemented | Awarded transition exists, but no source check/persistence for final awarding-letter requirements or certificate-equivalency approval. Test requires an approved specification/fixture for those requirements. |
| AWR-02 | University UI prompts to submit for Awarding Review. | `ChangeStatusAsync` permits University `Accepted → AwardingInProcess`. | Partially Implemented | No explicit source validation of government/admission-department requirement state before Awarded or Rejected. |
| AWR-03 | University rejection UI is available at awarding stage. | `AwardingInProcess → AwardingRejected` is permitted. `ApplyAsync` excludes rejected statuses from its active-application guard. | Missing | Rejected applicant can create a new application; this conflicts with client’s “blocked to apply again.” Test rejected application followed by candidate lookup/apply. |
| AWR-04 | No dedicated award-viewer implementation was established. | Generic application reads do not encode the DOCX’s per-status viewer marks. | Not Verifiable | Viewer details are absent/ambiguous in DOCX and must be clarified. |
| SPN-01 | `ngo-student-detail` offers Sponsor action while Sponsoring In Process. | `ChangeStatusAsync` permits NGO/Direct Aid role `SponsoringInProcess → Sponsored`, records generic history and persists status. | Implemented in source | Full-scholarship financial commitment is not represented as an amount/allocation; DOCX does not specify its data shape. Test status, actor, refresh, history. |
| SPN-02 | NGO detail exposes “Submit for Sponsorship Review.” | `ChangeStatusAsync` permits NGO `Awarded → SponsoringInProcess`. | Implemented in source | Source aligns on status sequence. Runtime role/status behavior is unverified. |
| SPN-03 | NGO detail exposes sponsorship rejection. | `ChangeStatusAsync` permits NGO `SponsoringInProcess → SponsoringRejected`; `ApplyAsync` does not block rejected applications. | Partially Implemented / Missing reapply block | Rejection status exists; mandatory no-reapplication rule is absent. Test rejection followed by application attempt. |
| SPN-04 | No Angular University final-awarding-letter upload control was found. The School Recommendation Letter upload is a different student-profile document. | No application/University API, DTO field, entity field or route was found for a final awarding letter after Sponsored. Existing `upload-document` is School application-document upload. | Missing | Add a separately approved final-awarding-letter artifact/authorization contract; do not reuse Recommendation Letter without client approval. |
| SPN-05 | No dedicated sponsoring-viewer policy UI was established. | Status service allows NGO decision; generic application retrieval has role logic but no per-status viewer matrix from DOCX. | Not Verifiable | Client needs to clarify viewer marks/visibility. |

## 5. Actual transition and permission matrix

| Current source status | Permitted next source status | Responsible source role | Conditions / enforcement | Endpoint | Persisted result | DOCX alignment |
|---|---|---|---|---|---|---|
| Draft | AcceptanceInProcess | School | School ownership; Draft only; all required `KfProgramDocument` records uploaded. Initial application window check intentionally disabled. | `POST api/school/student-program/submit/{applicationId}` | `ApplicationStatus`, submitted/audit fields; intended history event. | Partial: DOCX calls incomplete document state “In process,” source calls it Draft. |
| AcceptanceInProcess | Accepted / AcceptanceRejected | University | `ValidateApplicationAccess` confirms assigned University; legal transition check. | `PUT api/school/student-program/change-status/{applicationId}` | Application status, remarks if supplied, audit fields and history. | Partial: source term differs from “Not accepted”; no explicit requirements/reason validation. |
| Accepted | AwardingInProcess | University | Legal transition and University assignment. | Same status endpoint | Status/audit/history. | Matches stage handoff. |
| AwardingInProcess | Awarded / AwardingRejected | University | Legal transition and University assignment. | Same status endpoint | Status/audit/history. | Partial: no certificate-equivalency/final-awarding-letter requirement validation; rejection does not block reapplication. |
| Awarded | SponsoringInProcess | NGO / Direct Aid Committee role | Legal transition; NGO is allowed by `ValidateApplicationAccess`. | Same status endpoint | Status/audit/history. | Matches source sequence for client’s sponsoring-in-process definition. |
| SponsoringInProcess | Sponsored / SponsoringRejected | NGO / Direct Aid Committee role | Legal transition. | Same status endpoint | Status/audit/history. | Partial: decision transition exists; sponsoring rejection does not block reapplication; final-awarding-letter upload missing. |

`CanChangeStatus` is server-side and rejects jumps not shown above. The Angular NGO action controls call the status endpoint, but backend validation is the enforcement point. The source service allow-lists School, University and NGO in `ValidateApplicationAccess` for status transitions. No physical database constraint was verified for status sequencing.

## 6. Confirmed gaps, risks and items not verifiable from source

| Severity | Finding | Evidence | Required resolution |
|---|---|---|---|
| High | Awarding and sponsorship rejection do not block future application creation. | `ApplyAsync` active-status array omits `AwardingRejected` and `SponsoringRejected`; those statuses can pass its no-active-application guard. | Add approved server-side reapplication restriction for the client-required rejection statuses, then regression-test School UI and direct API. |
| High | Final awarding letter upload after Sponsored is missing. | Search found only School Recommendation Letter and generic School application-document upload; no University final-letter contract/path. | Define authorized post-Sponsored upload, storage, viewing and audit contract; implement separately after review. |
| Medium | Acceptance “In process” terminology differs from client document. | Source uses Draft while documents are incomplete, then AcceptanceInProcess only after all required documents. | Confirm whether client means Draft or requires a new/process-visible state; do not rename source enum without decision. |
| Medium | Awarding prerequisite validation is absent. | Awarding transitions are status-based; source does not represent government certificate equivalency or final awarding-letter requirements. | Client must define the fields/documents/evidence and rejection reasons to validate. |
| Medium | DOCX viewer matrix is ambiguous. | Viewer headers have no readable status marks in DOCX extraction. | Client must provide intended per-status viewer access; then audit/implement role/resource policy. |
| Medium | Application submission history may not be saved. | `SubmitApplicationAsync` adds history after `SaveChangesAsync` and commits without another `SaveChangesAsync`. | Fix and test in a later approved change; this audit does not modify source. |
| Medium | Application creation eligibility is not revalidated by `ApplyAsync`. | Candidate lookup calls `IsEligible`, while `ApplyAsync` has a TODO instead of that validator. | Revalidate in apply service before insert; add direct API tests. |
| Medium | Student creation action lacks explicit controller authorization. | `StudentsController.Create` has no `[Authorize]`; no observed fallback policy in `Program.cs`; service only restricts ownership for School staff. | Confirm middleware/global policy and enforce explicit role/ownership. |
| Unverified | Runtime authorization, transaction, storage and SQL constraint behavior. | No endpoint/database/browser action was executed. | Use approved non-production fixtures and metadata checks. |

## 7. Existing developer-reported test evidence

The developer reports the following as manually tested successfully. This audit did not repeat them:

- Student creation and editing.
- Student document upload.
- Student application submission.
- Application workflow through Acceptance In Process.

The next stage starts with an application currently at **Acceptance In Process**. Do not interpret these reports as proof of status authorization, reapplication blocking, final-letter upload, database constraints, or end-to-end registration behavior.

## 8. End-to-end test readiness checklist

Run only in an approved non-production environment, using a disposable student/application and authorized role accounts. Stop at the first unexpected persisted state.

| Test | Readiness | Expected evidence |
|---|---|---|
| Submit with every required document, then refresh/relogin. | Ready to Execute | Draft becomes `AcceptanceInProcess`; document list, audit fields and history persist. |
| Submit with a required document missing. | Ready to Execute | Server rejects submission; no acceptance-stage status transition. |
| Assigned University user accepts an `AcceptanceInProcess` application. | Ready to Execute | `Accepted` persists; School and University reads are consistent; application history/audit identify the action. |
| University creates awarding review and awards the student. | Ready to Execute | Legal `Accepted → AwardingInProcess → Awarded` sequence persists. |
| University rejects at acceptance or awarding. | Requires Business Clarification / partly Ready | Transition can be tested, but rejection-reason evidence and the client's intended “Not Accepted” terminology are unspecified. |
| Attempt a new application after `AwardingRejected`. | Blocked by Confirmed Gap | Current `ApplyAsync` does not enforce the required reapplication block. |
| Direct Aid Committee/NGO sends Awarded to sponsorship review, approves, then refreshes. | Ready to Execute | Legal `Awarded → SponsoringInProcess → Sponsored` status/audit/history is retrieved consistently. |
| Attempt a new application after `SponsoringRejected`. | Blocked by Confirmed Gap | Current source does not enforce the required reapplication block. |
| University uploads a final awarding letter after Sponsored. | Blocked by Confirmed Gap | No matching API/UI/entity contract was found. |
| Role visibility and unauthorized direct API calls. | Requires Business Clarification / runtime verification | Client viewer marks are ambiguous; source policy must be exercised with approved role accounts. |
| Invalid jumps and duplicate submit/apply requests. | Ready to Execute | Service rejects illegal status transitions; direct API tests establish duplicate behavior. |

## 9. Outstanding business clarifications

1. **Viewer access:** The DOCX lists viewer-role headings but has no readable per-status marks. The client must define which of School Coordinator, Direct Aid Committee, University and Marketing may view each status and its documents/actions.
2. **Acceptance in process:** Confirm whether the client calls an incomplete-document Draft application “In Process,” or whether the application must enter `AcceptanceInProcess` before documents are complete.
3. **Awarding evidence:** Define which government/university admission evidence, document(s), and rejection reason must be recorded before an Awarded or Awarding Rejected decision. The source currently supports only status/remarks.

## 10. Final readiness assessment and blockers

**Not ready for a full client-requirements end-to-end acceptance test.** The accepted/awarding/sponsoring status transitions are largely present in source, and developer-reported testing reaches Acceptance In Process. However, the mandatory rejection reapplication block and post-Sponsored final-awarding-letter upload are missing, client viewer permissions are unspecified, and awarding evidence requirements are not modeled. The source also contains application-submission history and direct-application eligibility risks that should be corrected before relying on the lifecycle.

The exact next end-to-end test, after fixes/clarifications are reviewed, is: use an approved disposable application currently in **Acceptance In Process** and have an assigned University user transition it to **Accepted**, then refresh it through the application read endpoint and UI to verify persisted status, audit data and history.

## Appendix A. Source evidence index

Line numbers are from the inspected working tree on 2 November 2026 and can shift with later edits.

| Area | Source evidence |
|---|---|
| Application API routes | `ScholarshipManagementAPI/Controllers/School/StudentProgramApplicationController.cs`: `Apply` line 43, `Submit` line 73, document upload line 113, status change line 223; each inspected application action is explicitly `[Authorize]`. |
| Application lifecycle and access | `ScholarshipManagementAPI/Services/Implementation/School/StudentProgramApplicationService.cs`: candidate eligibility use line 104; `ApplyAsync` line 141; `SubmitApplicationAsync` line 321; `ChangeStatusAsync` line 1138; `ValidateApplicationAccess` line 1322; `CanChangeStatus` line 1352; `IsEligible` line 1414. |
| Student-create authorization risk | `ScholarshipManagementAPI/Controllers/School/StudentsController.cs`: `Create` lines 27–28 has no `[Authorize]`; update begins with `[Authorize]` at line 44. |
| Sponsoring UI | `sms-ui/src/app/features/ngo/ngo-student-detail/ngo-student-detail.ts`: sponsoring review line 508, rejection line 541, approval immediately following. |
