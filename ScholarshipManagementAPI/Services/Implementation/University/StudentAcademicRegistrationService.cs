using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.SqlClient;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.University.AcademicRegistration;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.University;

namespace ScholarshipManagementAPI.Services.Implementation.University
{
    public class StudentAcademicRegistrationService : IStudentAcademicRegistrationService
    {
        private const string FinalizedSubmissionStatus = "Finalized";
        private const string CentralRegistrationRestrictionCode = "central-registration-window";
        private const string ProgramRegistrationRestrictionCode = "program-semester-registration-window";

        private readonly AppDbContext _context;

        public StudentAcademicRegistrationService(AppDbContext context)
        {
            _context = context;
        }


        public async Task<CourseEnrollmentSummaryDto> RegisterStudentAsync(RegisterStudentRequestDto dto, LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.University)
                throw new UnauthorizedAccessException();

            if (!currentUser.UniversityIds.Any())
                throw new UnauthorizedAccessException("User is not associated with a university.");

            // Serialize the duplicate check with concurrent initial-registration requests.
            // This is a code-level safeguard; a matching database uniqueness constraint still requires separate review.
            var ownsTransaction = _context.Database.CurrentTransaction == null;
            var transaction = ownsTransaction ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;

            try
            {
                var application = await _context.KfStudentProgramApplications
                    .Include(x => x.Program)
                    .FirstOrDefaultAsync(x => x.ApplicationId == dto.ApplicationId);

                if (application == null)
                    throw new CustomException("Student application not found.");

                if (!currentUser.UniversityIds.Contains(application.Program.UniversityId))
                    throw new UnauthorizedAccessException();

                if (application.ApplicationStatus != (int)StudentApplicationStatus.Sponsored)
                    throw new CustomException("Only sponsored students can be registered.");

                if (dto.SemesterNo < 1 || dto.SemesterNo > application.Program.NumberOfSemesters)
                    throw new CustomException("Invalid semester selected for this program.");

                var matchingSemesters = await _context.KfProgramSemesters
                    .AsNoTracking()
                    .Where(x => x.ProgramId == application.ProgramId && x.SemesterNo == dto.SemesterNo)
                    .Take(2)
                    .ToListAsync();

                if (matchingSemesters.Count == 0)
                    throw new CustomException("The selected semester is not configured for this program.");

                if (matchingSemesters.Count > 1)
                    throw new CustomException("Multiple program semesters match the selected semester. Please contact an administrator.");

                var programSemester = matchingSemesters[0];

                // TEMPORARILY DISABLED â€” DO NOT DELETE.
                // Business decision: assigning a program to a student is considered
                // sufficient to establish eligibility. Additional eligibility checks
                // during academic registration are therefore not required at this stage.
                // Keep this code for future reference in case the eligibility rules change.
                // var now = DateTime.UtcNow;
                // var centralRegistrationOpen = await _context.KfUniversityRegistrationWindows
                //     .AsNoTracking()
                //     .AnyAsync(x => x.UniversityId == application.Program.UniversityId &&
                //                    x.IsOpen &&
                //                    x.RegistrationFrom.HasValue &&
                //                    x.RegistrationFrom.Value <= now &&
                //                    x.RegistrationTo >= now);
                // if (!centralRegistrationOpen)
                //     throw new CustomException("University registration is not currently open.");

                var alreadyRegistered = await _context.KfStudentAcademicRegistrations
                    .AnyAsync(x => x.ApplicationId == dto.ApplicationId &&
                                   x.ProgramId == application.ProgramId &&
                                   x.SemesterNo == dto.SemesterNo);

                if (alreadyRegistered)
                    throw new CustomException("Student is already registered for this semester.");

                var academicRegistration = new KfStudentAcademicRegistration
                {
                    StudentId = application.StudentId,
                    ApplicationId = application.ApplicationId,
                    ProgramId = application.ProgramId,
                    SemesterNo = dto.SemesterNo,
                    ProgramSemesterId = programSemester.ProgramSemesterId,
                    RegistrationDate = dto.RegistrationDate,
                    Remarks = dto.Remarks,

                    CreatedBy = currentUser.LoginId,
                    CreatedOn = DateTime.UtcNow
                };

                _context.KfStudentAcademicRegistrations.Add(academicRegistration);
                await _context.SaveChangesAsync();

                var enrollmentSummary = await EnrollActiveCoursesAsync(
                    academicRegistration.Id,
                    academicRegistration.ProgramId,
                    academicRegistration.SemesterNo,
                    programSemester.ProgramSemesterId,
                    currentUser.LoginId);

                application.ApplicationStatus = (int)StudentApplicationStatus.Registered;
                application.UpdatedBy = currentUser.LoginId;
                application.UpdatedDate = DateTime.UtcNow;

                await AddHistoryAsync(
                    application.StudentId,
                    application.ApplicationId,
                    GetHistoryTitle(StudentApplicationStatus.Registered),
                    GetHistoryDescription(StudentApplicationStatus.Registered),
                    GetHistoryType(StudentApplicationStatus.Registered),
                    currentUser.LoginId);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return enrollmentSummary;
            }
            catch (Exception ex) when (IsSqlServerDeadlock(ex))
            {
                await transaction.RollbackAsync();
                throw new CustomException(
                    "Another registration request was processed concurrently. Refresh the student record before retrying.",
                    StatusCodes.Status409Conflict);
            }
            catch (DbUpdateException ex) when (IsAcademicRegistrationDuplicateConflict(ex))
            {
                await transaction.RollbackAsync();
                throw new CustomException("Student is already registered for this semester.", StatusCodes.Status409Conflict);
            }
            catch (DbUpdateException ex) when (IsCourseEnrollmentDuplicateConflict(ex))
            {
                await transaction.RollbackAsync();
                throw new CustomException("A course enrollment already exists for this academic registration. No registration changes were committed.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<AcademicRegistrationResultsDto> GetResultsAsync(
            long academicRegistrationId,
            LoggedInUserDto currentUser)
        {
            EnsureAcademicRecordReadAccess(currentUser);

            var registration = await GetRegistrationWithResultsAsync(academicRegistrationId, asNoTracking: true);
            if (registration == null)
                throw new CustomException("Academic registration not found.", StatusCodes.Status404NotFound);

            EnsureRegistrationReadAccess(registration, currentUser);
            ValidateRegistrationCourseMappings(registration);

            return MapAcademicRegistrationResults(registration);
        }

        public async Task<AcademicRegistrationResultsDto?> GetResultsByApplicationAsync(
            long applicationId,
            LoggedInUserDto currentUser)
        {
            EnsureAcademicRecordReadAccess(currentUser);

            var applicationProgramQuery = _context.KfStudentProgramApplications
                .AsNoTracking()
                .Where(x => x.ApplicationId == applicationId);

            if (currentUser.StaffType == StaffType.University)
            {
                applicationProgramQuery = applicationProgramQuery.Where(x =>
                    currentUser.UniversityIds.Contains(x.Program.UniversityId));
            }

            var applicationProgramId = await applicationProgramQuery
                .Select(x => (long?)x.ProgramId)
                .FirstOrDefaultAsync();

            if (!applicationProgramId.HasValue)
            {
                var fallbackQuery = _context.KfStudentProgramApplications
                    .AsNoTracking()
                    .Where(x => x.StudentId == applicationId);

                if (currentUser.StaffType == StaffType.University)
                {
                    fallbackQuery = fallbackQuery.Where(x =>
                        currentUser.UniversityIds.Contains(x.Program.UniversityId));
                }

                var fallbackApp = await fallbackQuery
                    .OrderByDescending(x => x.ApplicationId)
                    .Select(x => new { x.ApplicationId, x.ProgramId })
                    .FirstOrDefaultAsync();

                if (fallbackApp != null)
                {
                    applicationId = fallbackApp.ApplicationId;
                    applicationProgramId = fallbackApp.ProgramId;
                }
            }

            if (!applicationProgramId.HasValue)
                return null;

            var matchingIds = await _context.KfStudentAcademicRegistrations
                .AsNoTracking()
                .Where(x => x.ApplicationId == applicationId)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.ProgramId })
                .Take(2)
                .ToListAsync();

            if (matchingIds.Count == 0)
                return null;

            if (matchingIds.Count > 1)
                throw new CustomException(
                    "Multiple academic registrations exist for this application. The academic record cannot be selected safely.",
                    StatusCodes.Status409Conflict);

            if (matchingIds[0].ProgramId != applicationProgramId.Value)
                throw new CustomException(
                    "The academic registration does not match the application's program.",
                    StatusCodes.Status409Conflict);

            return await GetResultsAsync(matchingIds[0].Id, currentUser);
        }

        public async Task<AcademicHistoryDto?> GetAcademicHistoryByApplicationAsync(
            long applicationId,
            LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.University && currentUser.StaffType != StaffType.Ngo && !currentUser.IsSuperAdmin)
                throw new UnauthorizedAccessException();

            if (currentUser.StaffType == StaffType.University && !currentUser.UniversityIds.Any())
                throw new UnauthorizedAccessException("User is not associated with a university.");

            var anchorQuery = _context.KfStudentProgramApplications
                .AsNoTracking()
                .Where(x => x.ApplicationId == applicationId);

            if (currentUser.StaffType == StaffType.University)
            {
                anchorQuery = anchorQuery.Where(x => currentUser.UniversityIds.Contains(x.Program.UniversityId));
            }

            var anchorApplication = await anchorQuery
                .Select(x => new
                {
                    x.ApplicationId,
                    x.StudentId
                })
                .FirstOrDefaultAsync();

            if (anchorApplication == null)
            {
                var studentFallbackQuery = _context.KfStudentProgramApplications
                    .AsNoTracking()
                    .Where(x => x.StudentId == applicationId);

                if (currentUser.StaffType == StaffType.University)
                {
                    studentFallbackQuery = studentFallbackQuery.Where(x => currentUser.UniversityIds.Contains(x.Program.UniversityId));
                }

                anchorApplication = await studentFallbackQuery
                    .OrderByDescending(x => x.ApplicationId)
                    .Select(x => new
                    {
                        x.ApplicationId,
                        x.StudentId
                    })
                    .FirstOrDefaultAsync();
            }

            if (anchorApplication == null)
                return null;

            var regQuery = _context.KfStudentAcademicRegistrations
                .AsNoTracking()
                .Where(x => (x.StudentId == anchorApplication.StudentId ||
                             x.Application.StudentId == anchorApplication.StudentId));

            if (currentUser.StaffType == StaffType.University)
            {
                regQuery = regQuery.Where(x => currentUser.UniversityIds.Contains(x.Program.UniversityId));
            }

            var registrations = await regQuery
                .Include(x => x.Student)
                .Include(x => x.Application)
                    .ThenInclude(x => x.Program)
                .Include(x => x.Program)
                .Include(x => x.ProgramSemester)
                    .ThenInclude(x => x!.AcademicTerm)
                        .ThenInclude(x => x!.AcademicCalendar)
                .Include(x => x.KfStudentSemesterResult)
                .Include(x => x.KfStudentCourseEnrollments)
                    .ThenInclude(x => x.ProgramCourse)
                        .ThenInclude(x => x.Course)
                .Include(x => x.KfStudentCourseEnrollments)
                    .ThenInclude(x => x.ProgramCourse)
                        .ThenInclude(x => x.ProgramSemester)
                            .ThenInclude(x => x!.AcademicTerm)
                                .ThenInclude(x => x!.AcademicCalendar)
                .Include(x => x.KfStudentCourseEnrollments)
                    .ThenInclude(x => x.KfStudentCourseResult)
                .AsSplitQuery()
                .ToListAsync();

            foreach (var registration in registrations)
            {
                if (registration.StudentId != anchorApplication.StudentId ||
                    registration.Student == null ||
                    registration.Student.StudentId != registration.StudentId ||
                    registration.Application == null ||
                    registration.Application.StudentId != registration.StudentId ||
                    registration.Application.ProgramId != registration.ProgramId ||
                    registration.Program == null ||
                    (currentUser.StaffType == StaffType.University && (!currentUser.UniversityIds.Contains(registration.Program.UniversityId) || registration.Application.Program?.UniversityId != registration.Program.UniversityId)))
                {
                    throw new CustomException(
                        "Academic history contains inconsistent student, application, or program relationships.",
                        StatusCodes.Status409Conflict);
                }

                if (registration.ProgramSemesterId.HasValue &&
                    (registration.ProgramSemester == null ||
                     registration.ProgramSemester.ProgramId != registration.ProgramId ||
                     registration.ProgramSemester.SemesterNo != registration.SemesterNo ||
                     !IsAcademicTermValidForProgram(registration.ProgramSemester, registration.Program.UniversityId)))
                {
                    throw new CustomException(
                        "Academic history contains an invalid program-semester relationship.",
                        StatusCodes.Status409Conflict);
                }

                foreach (var enrollment in registration.KfStudentCourseEnrollments)
                {
                    var programCourse = enrollment.ProgramCourse;
                    if (programCourse == null ||
                        programCourse.ProgramId != registration.ProgramId ||
                        programCourse.SemesterNo != registration.SemesterNo ||
                        programCourse.Course == null)
                    {
                        throw new CustomException(
                            "Academic history contains a course that does not match its registration program and semester.",
                            StatusCodes.Status409Conflict);
                    }

                    if (programCourse.ProgramSemesterId.HasValue &&
                        (programCourse.ProgramSemester == null ||
                         programCourse.ProgramSemester.ProgramId != registration.ProgramId ||
                         programCourse.ProgramSemester.SemesterNo != registration.SemesterNo ||
                         !IsAcademicTermValidForProgram(programCourse.ProgramSemester, registration.Program.UniversityId) ||
                         (registration.ProgramSemesterId.HasValue &&
                          programCourse.ProgramSemesterId != registration.ProgramSemesterId)))
                    {
                        throw new CustomException(
                            "Academic history contains a conflicting course-semester relationship.",
                            StatusCodes.Status409Conflict);
                    }
                }
            }

            var programIds = registrations.Select(x => x.ProgramId).Distinct().ToList();
            var semesterNumbers = registrations.Select(x => x.SemesterNo).Distinct().ToList();
            var unresolvedRegistrationKeys = registrations
                .Where(x => !x.ProgramSemesterId.HasValue)
                .Select(x => new { x.ProgramId, x.SemesterNo })
                .Distinct()
                .ToList();

            var semesterCandidates = unresolvedRegistrationKeys.Count == 0
                ? new List<KfProgramSemester>()
                : await _context.KfProgramSemesters
                    .AsNoTracking()
                    .Where(x => programIds.Contains(x.ProgramId) &&
                                semesterNumbers.Contains(x.SemesterNo))
                    .Include(x => x.AcademicTerm)
                        .ThenInclude(x => x!.AcademicCalendar)
                    .ToListAsync();

            var candidatesByProgramSemester = semesterCandidates
                .GroupBy(x => (x.ProgramId, x.SemesterNo))
                .ToDictionary(x => x.Key, x => x.ToList());

            var mappedRegistrations = registrations.Select(registration =>
            {
                var issues = new List<string>();
                KfProgramSemester? semester = registration.ProgramSemester;
                var scheduleLinkStatus = registration.ProgramSemesterId.HasValue
                    ? "Resolved"
                    : "Unresolved";

                if (!registration.ProgramSemesterId.HasValue)
                {
                    candidatesByProgramSemester.TryGetValue(
                        (registration.ProgramId, registration.SemesterNo),
                        out var candidates);

                    var validCandidates = candidates?
                        .Where(x => IsAcademicTermValidForProgram(x, registration.Program.UniversityId))
                        .ToList() ?? new List<KfProgramSemester>();

                    if (validCandidates.Count == 1)
                    {
                        semester = validCandidates[0];
                        scheduleLinkStatus = "ResolvedLegacy";
                        if (candidates!.Count > validCandidates.Count)
                            issues.Add("One or more matching program-semester rows have invalid academic-term ownership and were ignored.");
                    }
                    else if (validCandidates.Count > 1)
                    {
                        scheduleLinkStatus = "Ambiguous";
                        issues.Add("More than one persisted program semester matches this program and semester number.");
                    }
                    else
                    {
                        issues.Add(candidates?.Count > 0
                            ? "Matching program-semester rows have invalid academic-term ownership."
                            : "No persisted program semester matches this program and semester number.");
                    }
                }

                var courseDtos = new List<AcademicHistoryCourseDto>();
                foreach (var enrollment in registration.KfStudentCourseEnrollments
                    .OrderBy(x => x.ProgramCourse.DisplayOrder)
                    .ThenBy(x => x.ProgramCourse.ProgramCourseId)
                    .ThenBy(x => x.Id))
                {
                    var programCourse = enrollment.ProgramCourse;
                    var courseSemesterStatus = enrollment.RetakeCaseCourseId.HasValue
                        ? "Retake"
                        : programCourse.ProgramSemesterId.HasValue
                            ? "Resolved"
                            : registration.ProgramSemesterId.HasValue
                                ? "ResolvedByRegistrationSemester"
                                : "ResolvedByProgramAndSemester";

                    if (!enrollment.RetakeCaseCourseId.HasValue &&
                        !programCourse.ProgramSemesterId.HasValue &&
                        !registration.ProgramSemesterId.HasValue &&
                        (!candidatesByProgramSemester.TryGetValue((registration.ProgramId, registration.SemesterNo), out var matchingSemesters) ||
                         matchingSemesters.Count(x => IsAcademicTermValidForProgram(x, registration.Program.UniversityId)) != 1))
                    {
                        courseSemesterStatus = "Unresolved";
                        issues.Add($"Course enrollment {enrollment.Id} has no unambiguous persisted semester mapping.");
                    }

                    courseDtos.Add(new AcademicHistoryCourseDto
                    {
                        StudentCourseEnrollmentId = enrollment.Id,
                        ProgramCourseId = programCourse.ProgramCourseId,
                        CourseId = programCourse.CourseId,
                        CourseCode = programCourse.Course.CourseCode,
                        CourseName = programCourse.Course.CourseNameEn,
                        CourseType = programCourse.CourseType,
                        Credits = programCourse.Credits,
                        IsRetake = enrollment.RetakeCaseCourseId.HasValue,
                        IsPassed = enrollment.KfStudentCourseResult?.IsPassed,
                        Grade = enrollment.KfStudentCourseResult?.Grade,
                        Remarks = enrollment.KfStudentCourseResult?.Remarks,
                        ResultRecordedOn = enrollment.KfStudentCourseResult?.RecordedOn,
                        ResultUpdatedOn = enrollment.KfStudentCourseResult?.UpdatedOn,
                        ProgramSemesterLinkStatus = courseSemesterStatus
                    });
                }

                return new AcademicHistoryRegistrationDto
                {
                    AcademicRegistrationId = registration.Id,
                    ApplicationId = registration.ApplicationId,
                    ProgramId = registration.ProgramId,
                    ProgramCode = registration.Program.ProgramCode,
                    ProgramName = registration.Program.ProgramName,
                    SemesterNo = registration.SemesterNo,
                    RegistrationDate = registration.RegistrationDate,
                    ProgramSemesterId = registration.ProgramSemesterId,
                    ScheduleLinkStatus = scheduleLinkStatus,
                    SemesterName = semester?.SemesterName,
                    AcademicTermId = semester?.AcademicTermId,
                    AcademicCalendarId = semester?.AcademicTerm?.AcademicCalendarId,
                    TermSequence = semester?.AcademicTerm?.TermSequence,
                    TermName = semester?.AcademicTerm?.TermName,
                    AcademicYearName = semester?.AcademicTerm?.AcademicCalendar?.AcademicYearName,
                    StartDate = semester?.StartDate,
                    EndDate = semester?.EndDate,
                    DataIssues = issues,
                    SemesterResult = registration.KfStudentSemesterResult == null
                        ? null
                        : new AcademicHistorySemesterResultDto
                        {
                            SemesterGpa = registration.KfStudentSemesterResult.SemesterGpa,
                            SubmissionStatus = registration.KfStudentSemesterResult.SubmissionStatus,
                            CreatedOn = registration.KfStudentSemesterResult.CreatedOn,
                            UpdatedOn = registration.KfStudentSemesterResult.UpdatedOn ?? registration.KfStudentSemesterResult.CreatedOn,
                            SubmittedOn = registration.KfStudentSemesterResult.SubmittedOn
                        },
                    Courses = courseDtos
                };
            }).ToList();

            var orderedRegistrations = mappedRegistrations
                .GroupBy(x => x.ProgramId)
                .Select(group => new
                {
                    ProgramId = group.Key,
                    FirstDate = group.Where(x => x.StartDate.HasValue)
                        .Select(x => x.StartDate!.Value)
                        .DefaultIfEmpty(group.Min(x => x.RegistrationDate))
                        .Min(),
                    Registrations = group
                        .OrderBy(x => x.SemesterNo)
                        .ThenBy(x => x.RegistrationDate)
                        .ThenBy(x => x.AcademicRegistrationId)
                        .ToList()
                })
                .OrderBy(x => x.FirstDate)
                .ThenBy(x => x.ProgramId)
                .SelectMany(x => x.Registrations)
                .ToList();

            return new AcademicHistoryDto
            {
                AnchorApplicationId = anchorApplication.ApplicationId,
                StudentId = anchorApplication.StudentId,
                Registrations = orderedRegistrations
            };
        }

        public async Task<AcademicResultsSaveSummaryDto> SaveResultsAsync(
            long academicRegistrationId,
            SaveAcademicResultsRequestDto dto,
            LoggedInUserDto currentUser)
        {
            EnsureUniversityAccess(currentUser);

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            var transaction = ownsTransaction ? await _context.Database.BeginTransactionAsync() : null;

            try
            {
                var registration = await GetRegistrationWithResultsAsync(academicRegistrationId, asNoTracking: false);
                if (registration == null)
                    throw new CustomException("Academic registration not found.", StatusCodes.Status404NotFound);

                EnsureRegistrationUniversityAccess(registration, currentUser);
                ValidateRegistrationCourseMappings(registration);

                if (registration.KfStudentCourseEnrollments.Count == 0)
                    throw new CustomException("No courses are enrolled in this academic registration.");

                if (dto.CourseResults == null)
                    throw new CustomException("Course results are required.");

                if (dto.CourseResults.Any(x => x == null))
                    throw new CustomException("Course results cannot contain empty entries.");

                if (!dto.SemesterGpa.HasValue)
                {
                    throw new CustomException("Enter the semester GPA (0.00 – 4.00).", StatusCodes.Status400BadRequest);
                }

                if (dto.SemesterGpa.Value < 0m || dto.SemesterGpa.Value > 4m ||
                    decimal.Round(dto.SemesterGpa.Value, 2) != dto.SemesterGpa.Value)
                {
                    throw new CustomException("Semester GPA must be between 0.00 and 4.00 with no more than two decimal places.", StatusCodes.Status400BadRequest);
                }

                var internalNote = string.IsNullOrWhiteSpace(dto.InternalNote)
                    ? null
                    : dto.InternalNote.Trim();
                if (internalNote?.Length > 1000)
                    throw new CustomException("The semester note cannot exceed 1,000 characters.");

                var enrolledIds = registration.KfStudentCourseEnrollments
                    .Select(x => x.Id)
                    .ToHashSet();
                var duplicateIds = dto.CourseResults
                    .GroupBy(x => x.StudentCourseEnrollmentId)
                    .Any(x => x.Count() > 1);

                if (duplicateIds)
                    throw new CustomException("Duplicate course-result entries were submitted.");

                if (dto.CourseResults.Count != enrolledIds.Count ||
                    dto.CourseResults.Any(x => !enrolledIds.Contains(x.StudentCourseEnrollmentId)))
                {
                    throw new CustomException("Submit exactly one result for every course enrolled in this academic registration.");
                }

                if (dto.CourseResults.Any(x => !x.IsPassed.HasValue))
                    throw new CustomException("Every enrolled course must have a Pass or Fail result.");

                var requestedResults = dto.CourseResults.ToDictionary(x => x.StudentCourseEnrollmentId);
                var existingCourseResults = registration.KfStudentCourseEnrollments
                    .Where(x => x.KfStudentCourseResult != null)
                    .ToList();
                var semesterResult = registration.KfStudentSemesterResult;

                if (existingCourseResults.Count > 0 || semesterResult != null)
                {
                    if (existingCourseResults.Count != registration.KfStudentCourseEnrollments.Count || semesterResult == null)
                    {
                        throw new CustomException(
                            "Existing academic results are incomplete. They were preserved and were not overwritten.");
                    }

                    if (!string.Equals(semesterResult.SubmissionStatus, "Draft", StringComparison.Ordinal))
                    {
                        throw new CustomException(
                            "These academic results are no longer editable.",
                            StatusCodes.Status409Conflict);
                    }

                    var currentTimestamp = semesterResult.UpdatedOn ?? semesterResult.CreatedOn;
                    if (!TimestampsMatch(dto.ExpectedUpdatedOn, currentTimestamp))
                        throw new CustomException(
                            "These draft results changed after they were loaded. Reload the latest results before saving again.",
                            StatusCodes.Status409Conflict);

                    var valuesMatch = registration.KfStudentCourseEnrollments.All(enrollment =>
                        enrollment.KfStudentCourseResult!.IsPassed ==
                        requestedResults[enrollment.Id].IsPassed) &&
                        semesterResult.SemesterGpa == dto.SemesterGpa &&
                        string.Equals(semesterResult.InternalNote, internalNote, StringComparison.Ordinal);

                    if (valuesMatch)
                    {
                        await transaction.CommitAsync();
                        return new AcademicResultsSaveSummaryDto
                        {
                            AcademicRegistrationId = registration.Id,
                            CourseResultsCreated = 0,
                            CourseResultsAlreadyPresent = existingCourseResults.Count,
                            SemesterResultCreated = false,
                            SubmissionStatus = semesterResult.SubmissionStatus,
                            ExpectedUpdatedOn = semesterResult.UpdatedOn ?? semesterResult.CreatedOn
                        };
                    }

                    var updateTime = DateTime.UtcNow;
                    if (semesterResult.UpdatedOn.HasValue &&
                        updateTime <= semesterResult.UpdatedOn.Value.AddMilliseconds(10))
                    {
                        updateTime = semesterResult.UpdatedOn.Value.AddMilliseconds(10);
                    }
                    semesterResult.SemesterGpa = dto.SemesterGpa;
                    semesterResult.InternalNote = internalNote;
                    semesterResult.UpdatedBy = currentUser.LoginId;
                    semesterResult.UpdatedOn = updateTime;

                    foreach (var enrollment in registration.KfStudentCourseEnrollments)
                    {
                        var courseResult = enrollment.KfStudentCourseResult!;
                        var requestedPassStatus = requestedResults[enrollment.Id].IsPassed;
                        if (courseResult.IsPassed != requestedPassStatus)
                        {
                            courseResult.IsPassed = requestedPassStatus;
                            courseResult.RecordedBy = currentUser.LoginId;
                            courseResult.RecordedOn = updateTime;
                            courseResult.UpdatedBy = currentUser.LoginId;
                            courseResult.UpdatedOn = updateTime;
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new AcademicResultsSaveSummaryDto
                    {
                        AcademicRegistrationId = registration.Id,
                        CourseResultsCreated = 0,
                        CourseResultsAlreadyPresent = existingCourseResults.Count,
                        SemesterResultCreated = false,
                        SubmissionStatus = semesterResult.SubmissionStatus
                    };
                }

                if (dto.ExpectedUpdatedOn.HasValue)
                    throw new CustomException(
                        "The academic results changed after they were loaded. Reload the academic record before saving again.",
                        StatusCodes.Status409Conflict);

                var now = DateTime.UtcNow;
                _context.KfStudentCourseResults.AddRange(registration.KfStudentCourseEnrollments.Select(enrollment =>
                    new KfStudentCourseResult
                    {
                        StudentCourseEnrollmentId = enrollment.Id,
                        IsPassed = requestedResults[enrollment.Id].IsPassed,
                        RecordedBy = currentUser.LoginId,
                        RecordedOn = now,
                        CreatedBy = currentUser.LoginId,
                        CreatedOn = now
                    }));

                _context.KfStudentSemesterResults.Add(new KfStudentSemesterResult
                {
                    AcademicRegistrationId = registration.Id,
                    SemesterGpa = dto.SemesterGpa,
                    InternalNote = internalNote,
                    SubmissionStatus = "Draft",
                    UpdatedBy = currentUser.LoginId,
                    UpdatedOn = now,
                    CreatedBy = currentUser.LoginId,
                    CreatedOn = now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new AcademicResultsSaveSummaryDto
                {
                    AcademicRegistrationId = registration.Id,
                    CourseResultsCreated = registration.KfStudentCourseEnrollments.Count,
                    CourseResultsAlreadyPresent = 0,
                    SemesterResultCreated = true,
                    SubmissionStatus = "Draft",
                    ExpectedUpdatedOn = now
                };
            }
            catch (DbUpdateException ex) when (IsAcademicResultDuplicateConflict(ex))
            {
                await transaction.RollbackAsync();
                throw new CustomException(
                    "Another results request was saved concurrently. Reload the academic record and retry; no partial result changes were committed.",
                    StatusCodes.Status409Conflict);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                throw new CustomException(
                    "These draft results changed after they were loaded. Reload the latest results before saving again.",
                    StatusCodes.Status409Conflict);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<AcademicResultsImportPreviewDto> PreviewResultsImportAsync(
            IFormFile file,
            LoggedInUserDto currentUser)
        {
            var analysis = await AnalyzeResultsImportAsync(file, currentUser);
            return analysis.Preview;
        }

        public async Task<AcademicResultsSaveSummaryDto> ImportResultsAsync(
            IFormFile file,
            string? previewToken,
            DateTime? expectedUpdatedOn,
            LoggedInUserDto currentUser)
        {
            var analysis = await AnalyzeResultsImportAsync(file, currentUser);
            if (!analysis.Preview.CanSubmit || analysis.SaveRequest == null)
                throw new CustomException(analysis.Preview.Message ?? "The import contains validation errors.");
            if (string.IsNullOrWhiteSpace(previewToken) || previewToken.Length != 64 ||
                previewToken.Any(c => !Uri.IsHexDigit(c)) ||
                !CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(previewToken),
                    Convert.FromHexString(analysis.Preview.PreviewToken!)))
                throw new CustomException("The workbook changed after preview. Validate the selected file again.", StatusCodes.Status409Conflict);

            // SaveResultsAsync repeats authorization, enrollment-set, Draft, and concurrency
            // validation immediately before its atomic persistence operation.
            analysis.SaveRequest.ExpectedUpdatedOn = expectedUpdatedOn;
            return await SaveResultsAsync(analysis.Preview.AcademicRegistrationId, analysis.SaveRequest, currentUser);
        }

        private async Task<(AcademicResultsImportPreviewDto Preview, SaveAcademicResultsRequestDto? SaveRequest)> AnalyzeResultsImportAsync(
            IFormFile file,
            LoggedInUserDto currentUser)
        {
            EnsureUniversityAccess(currentUser);
            if (file == null || file.Length == 0 || file.Length > AcademicResultsWorkbook.MaxFileBytes ||
                !string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                throw new CustomException("Select a non-empty .xlsx workbook no larger than 5 MB.");

            await using var workbookBytes = new MemoryStream();
            await file.CopyToAsync(workbookBytes);
            var previewToken = Convert.ToHexString(SHA256.HashData(workbookBytes.ToArray()));
            workbookBytes.Position = 0;
            var workbook = AcademicResultsWorkbook.Parse(workbookBytes);
            if (!long.TryParse(workbook.Rows.FirstOrDefault()?.AcademicRegistrationId, NumberStyles.None, CultureInfo.InvariantCulture, out var academicRegistrationId) || academicRegistrationId <= 0)
                throw new CustomException("The first result row must contain a valid AcademicRegistrationId.");
            var registration = await GetRegistrationWithResultsAsync(academicRegistrationId, asNoTracking: true);
            if (registration == null)
                throw new CustomException("Academic registration not found.", StatusCodes.Status404NotFound);
            EnsureRegistrationUniversityAccess(registration, currentUser);
            ValidateRegistrationCourseMappings(registration);

            var preview = new AcademicResultsImportPreviewDto
            {
                AcademicRegistrationId = academicRegistrationId,
                TotalRows = workbook.Rows.Count,
                ExpectedUpdatedOn = registration.KfStudentSemesterResult?.UpdatedOn ?? registration.KfStudentSemesterResult?.CreatedOn,
                PreviewToken = previewToken,
                Message = workbook.FileErrors.FirstOrDefault()
            };
            var globalErrors = new List<string>(workbook.FileErrors);
            if (registration.KfStudentCourseEnrollments.Count == 0)
                globalErrors.Add("This registration has no enrolled courses.");
            if (string.Equals(registration.KfStudentSemesterResult?.SubmissionStatus, "Finalized", StringComparison.Ordinal))
                globalErrors.Add("Finalized results are read-only and cannot be overwritten.");
            if (registration.KfStudentSemesterResult != null &&
                !string.Equals(registration.KfStudentSemesterResult.SubmissionStatus, "Draft", StringComparison.Ordinal))
                globalErrors.Add("Only Draft results can be updated.");
            if (registration.KfStudentSemesterResult != null &&
                !registration.KfStudentSemesterResult.UpdatedOn.HasValue)
                globalErrors.Add("This existing Draft has no concurrency timestamp. Save or reload it through Academic Record before importing.");
            var savedImportCourseResults = registration.KfStudentCourseEnrollments.Count(x => x.KfStudentCourseResult != null);
            if ((savedImportCourseResults > 0 || registration.KfStudentSemesterResult != null) &&
                (savedImportCourseResults != registration.KfStudentCourseEnrollments.Count || registration.KfStudentSemesterResult == null))
                globalErrors.Add("Existing results are incomplete and were preserved. Resolve them through Academic Record before importing.");

            var courseByCode = registration.KfStudentCourseEnrollments
                .GroupBy(x => x.ProgramCourse.Course.CourseCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.OrdinalIgnoreCase);
            var duplicateCodes = workbook.Rows
                .Where(x => !string.IsNullOrWhiteSpace(x.CourseCode))
                .GroupBy(x => x.CourseCode!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(x => x.Count() > 1)
                .SelectMany(x => x)
                .Select(x => x.RowNumber)
                .ToHashSet();

            decimal? sharedGpa = null;
            var normalizedGpas = new List<decimal?>();
            string? sharedNote = null;
            var normalizedNotes = new List<string?>();
            var gpaParseFailed = false;
            foreach (var row in workbook.Rows)
            {
                if (string.IsNullOrWhiteSpace(row.SemesterGpa)) normalizedGpas.Add(null);
                else if (decimal.TryParse(row.SemesterGpa, NumberStyles.Number, CultureInfo.InvariantCulture, out var gpa)) normalizedGpas.Add(gpa);
                else { normalizedGpas.Add(null); gpaParseFailed = true; }
                normalizedNotes.Add(string.IsNullOrWhiteSpace(row.InternalNote) ? null : row.InternalNote.Trim());
            }
            if (normalizedGpas.Distinct().Count() > 1)
                globalErrors.Add("SemesterGpa must be blank or have the same value on every data row.");
            if (normalizedNotes.Distinct(StringComparer.Ordinal).Count() > 1)
                globalErrors.Add("InternalNote must be blank or have the same value on every data row.");
            if (gpaParseFailed) globalErrors.Add("SemesterGpa must be a numeric value using a period as the decimal separator.");
            sharedGpa = normalizedGpas.FirstOrDefault();
            sharedNote = normalizedNotes.FirstOrDefault();
            if (sharedGpa.HasValue && (sharedGpa < 0m || sharedGpa > 4m || decimal.Round(sharedGpa.Value, 2) != sharedGpa.Value))
                globalErrors.Add("SemesterGpa must be between 0.00 and 4.00 with no more than two decimal places.");
            if (sharedNote?.Length > 1000) globalErrors.Add("InternalNote cannot exceed 1,000 characters.");

            var mappedResults = new List<SaveCourseResultRequestDto>();
            foreach (var row in workbook.Rows)
            {
                var errors = new List<string>(row.Errors);
                if (!long.TryParse(row.AcademicRegistrationId, NumberStyles.None, CultureInfo.InvariantCulture, out var rowRegistrationId) ||
                    rowRegistrationId != academicRegistrationId)
                    errors.Add("AcademicRegistrationId must match the registration being imported.");
                if (string.IsNullOrWhiteSpace(row.CourseCode)) errors.Add("CourseCode is required.");
                else if (!courseByCode.TryGetValue(row.CourseCode.Trim(), out var matches)) errors.Add("CourseCode is not enrolled in this academic registration.");
                else if (matches.Count != 1) errors.Add("CourseCode is ambiguous within this academic registration.");
                if (duplicateCodes.Contains(row.RowNumber)) errors.Add("The workbook contains this CourseCode more than once.");

                bool? passed = string.Equals(row.Result?.Trim(), "Pass", StringComparison.OrdinalIgnoreCase) ? true :
                    string.Equals(row.Result?.Trim(), "Fail", StringComparison.OrdinalIgnoreCase) ? false : null;
                if (!passed.HasValue) errors.Add("Result must be Pass or Fail.");

                if (errors.Count == 0 && !string.IsNullOrWhiteSpace(row.CourseCode) &&
                    courseByCode.TryGetValue(row.CourseCode.Trim(), out var enrollmentMatches) && enrollmentMatches.Count == 1)
                {
                    mappedResults.Add(new SaveCourseResultRequestDto
                    {
                        StudentCourseEnrollmentId = enrollmentMatches[0].Id,
                        IsPassed = passed
                    });
                }
                preview.Rows.Add(new AcademicResultsImportRowDto
                {
                    RowNumber = row.RowNumber,
                    CourseCode = row.CourseCode,
                    Result = row.Result,
                    IsValid = errors.Count == 0,
                    Errors = errors
                });
            }

            var mappedCodes = workbook.Rows.Where(x => !string.IsNullOrWhiteSpace(x.CourseCode))
                .Select(x => x.CourseCode!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var enrollment in registration.KfStudentCourseEnrollments)
            {
                var code = enrollment.ProgramCourse.Course.CourseCode.Trim();
                if (!mappedCodes.Contains(code)) globalErrors.Add($"Missing result row for enrolled course '{code}'.");
            }
            if (mappedResults.Count != registration.KfStudentCourseEnrollments.Count)
                globalErrors.Add("The workbook must contain exactly one valid result for every enrolled course.");

            preview.ValidRows = preview.Rows.Count(x => x.IsValid);
            preview.InvalidRows = preview.TotalRows - preview.ValidRows;
            if (globalErrors.Count > 0)
                preview.Message = string.Join(" ", globalErrors.Distinct(StringComparer.Ordinal));
            preview.CanSubmit = preview.TotalRows > 0 && preview.InvalidRows == 0 && globalErrors.Count == 0;
            if (preview.CanSubmit)
            {
                preview.Message = "The complete workbook is valid and can be saved as a Draft.";
                return (preview, new SaveAcademicResultsRequestDto
                {
                    SemesterGpa = sharedGpa,
                    InternalNote = sharedNote,
                    ExpectedUpdatedOn = registration.KfStudentSemesterResult?.UpdatedOn ?? registration.KfStudentSemesterResult?.CreatedOn,
                    CourseResults = mappedResults
                });
            }
            if (string.IsNullOrWhiteSpace(preview.Message)) preview.Message = "Correct the workbook errors before submitting.";
            return (preview, null);
        }

        public async Task<AcademicRegistrationResultsDto> FinalizeResultsAsync(
            long academicRegistrationId,
            FinalizeAcademicResultsRequestDto dto,
            LoggedInUserDto currentUser)
        {
            EnsureUniversityAccess(currentUser);

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            var transaction = ownsTransaction ? await _context.Database.BeginTransactionAsync() : null;

            try
            {
                var registration = await GetRegistrationWithResultsAsync(academicRegistrationId, asNoTracking: false);
                if (registration == null)
                    throw new CustomException("Academic registration not found.", StatusCodes.Status404NotFound);

                EnsureRegistrationUniversityAccess(registration, currentUser);
                ValidateRegistrationCourseMappings(registration);

                var semesterResult = registration.KfStudentSemesterResult;
                if (semesterResult == null)
                    throw new CustomException("Save the semester results as a Draft before finalizing them.");

                if (!semesterResult.SemesterGpa.HasValue || semesterResult.SemesterGpa.Value < 0m || semesterResult.SemesterGpa.Value > 4m)
                {
                    throw new CustomException("Enter the semester GPA (0.00 – 4.00).", StatusCodes.Status400BadRequest);
                }

                if (!string.Equals(semesterResult.SubmissionStatus, "Draft", StringComparison.Ordinal))
                {
                    throw new CustomException(
                        "These semester results are no longer editable or cannot be finalized again.",
                        StatusCodes.Status409Conflict);
                }

                if (semesterResult.SubmittedBy.HasValue || semesterResult.SubmittedOn.HasValue)
                {
                    throw new CustomException(
                        "The Draft contains prior submission metadata and cannot be finalized safely.",
                        StatusCodes.Status409Conflict);
                }

                var currentFinalizeTimestamp = semesterResult.UpdatedOn ?? semesterResult.CreatedOn;
                if (!TimestampsMatch(dto.ExpectedUpdatedOn, currentFinalizeTimestamp))
                {
                    throw new CustomException(
                        "These Draft results changed after they were loaded. Reload the latest results before finalizing.",
                        StatusCodes.Status409Conflict);
                }

                var enrollmentIds = registration.KfStudentCourseEnrollments
                    .Select(x => x.Id)
                    .ToHashSet();
                if (enrollmentIds.Count == 0)
                    throw new CustomException("No courses are enrolled in this academic registration.");

                var savedCourseResults = await _context.KfStudentCourseResults
                    .AsNoTracking()
                    .Where(x => enrollmentIds.Contains(x.StudentCourseEnrollmentId))
                    .Select(x => new { x.Id, x.StudentCourseEnrollmentId, x.IsPassed })
                    .ToListAsync();

                var resultsByEnrollment = savedCourseResults
                    .GroupBy(x => x.StudentCourseEnrollmentId)
                    .ToDictionary(x => x.Key, x => x.ToList());

                if (savedCourseResults.Count != enrollmentIds.Count ||
                    enrollmentIds.Any(id => !resultsByEnrollment.TryGetValue(id, out var rows) ||
                                            rows.Count != 1 ||
                                            !rows[0].IsPassed.HasValue))
                {
                    throw new CustomException(
                        "Every enrolled course must have exactly one saved Pass or Fail outcome before finalization.");
                }

                var finalizedOn = DateTime.UtcNow;
                if (semesterResult.UpdatedOn.HasValue &&
                    finalizedOn <= semesterResult.UpdatedOn.Value.AddMilliseconds(10))
                {
                    finalizedOn = semesterResult.UpdatedOn.Value.AddMilliseconds(10);
                }

                var failedCoreResults = registration.KfStudentCourseEnrollments
                    .Where(enrollment => resultsByEnrollment[enrollment.Id][0].IsPassed == false &&
                                         enrollment.ProgramCourse.CourseType == (byte)ProgramCourseTypeEnum.Core)
                    .Select(enrollment => resultsByEnrollment[enrollment.Id][0].Id)
                    .ToList();

                if (failedCoreResults.Count > 0)
                {
                    var existingCase = await _context.KfStudentFailingCoreCases
                        .AsNoTracking()
                        .AnyAsync(x => x.AcademicRegistrationId == registration.Id);
                    if (existingCase)
                        throw new CustomException(
                            "A failing-core case already exists for this academic registration. Review the case before finalizing again.",
                            StatusCodes.Status409Conflict);

                    var newCase = new KfStudentFailingCoreCase
                    {
                        AcademicRegistrationId = registration.Id,
                        Status = "Pending",
                        SubmittedOn = finalizedOn,
                        CreatedBy = currentUser.LoginId,
                        CreatedOn = finalizedOn
                    };
                    newCase.KfStudentFailingCoreCaseCourses = failedCoreResults.Select(resultId => new KfStudentFailingCoreCaseCourse
                    {
                        StudentCourseResultId = resultId,
                        CreatedBy = currentUser.LoginId,
                        CreatedOn = newCase.SubmittedOn
                    }).ToList();
                    _context.KfStudentFailingCoreCases.Add(newCase);
                }

                semesterResult.SubmissionStatus = FinalizedSubmissionStatus;
                semesterResult.SubmittedBy = currentUser.LoginId;
                semesterResult.SubmittedOn = finalizedOn;
                semesterResult.UpdatedBy = currentUser.LoginId;
                semesterResult.UpdatedOn = finalizedOn;

                await _context.SaveChangesAsync();

                // Return the value as persisted, including any SQL Server datetime precision rounding.
                await _context.Entry(semesterResult).ReloadAsync();
                if (ownsTransaction && transaction != null)
                {
                    await transaction.CommitAsync();
                }

                return MapAcademicRegistrationResults(registration);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (ownsTransaction && transaction != null)
                    await TryRollbackFinalizationAsync(transaction);
                throw new CustomException(
                    "These Draft results changed concurrently. Reload the latest results before finalizing.",
                    StatusCodes.Status409Conflict);
            }
            catch (DbUpdateException ex) when (IsFailingCoreCaseDuplicateConflict(ex))
            {
                if (ownsTransaction && transaction != null)
                    await TryRollbackFinalizationAsync(transaction);
                throw new CustomException(
                    "A failing-core case was created concurrently for this academic registration. Reload the academic record.",
                    StatusCodes.Status409Conflict);
            }
            catch
            {
                if (ownsTransaction && transaction != null)
                    await TryRollbackFinalizationAsync(transaction);
                throw;
            }
        }

        public async Task<AcademicProgressionResponseDto> ProgressToNextSemesterAsync(
            long academicRegistrationId,
            ProgressAcademicRegistrationRequestDto dto,
            LoggedInUserDto currentUser)
        {
            EnsureUniversityAccess(currentUser);

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            var transaction = ownsTransaction ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;

            try
            {
                var registration = await _context.KfStudentAcademicRegistrations
                    .Include(x => x.Student)
                    .Include(x => x.Program)
                    .Include(x => x.Application)
                    .Include(x => x.ProgramSemester)
                        .ThenInclude(x => x!.AcademicTerm)
                            .ThenInclude(x => x!.AcademicCalendar)
                    .Include(x => x.KfStudentSemesterResult)
                    .Include(x => x.KfStudentCourseEnrollments)
                        .ThenInclude(x => x.ProgramCourse)
                            .ThenInclude(x => x.Course)
                    .Include(x => x.KfStudentCourseEnrollments)
                        .ThenInclude(x => x.ProgramCourse)
                            .ThenInclude(x => x.ProgramSemester)
                                .ThenInclude(x => x!.AcademicTerm)
                                    .ThenInclude(x => x!.AcademicCalendar)
                    .Include(x => x.KfStudentCourseEnrollments)
                        .ThenInclude(x => x.RetakeCaseCourse)
                            .ThenInclude(x => x!.FailingCoreCase)
                                .ThenInclude(x => x.AcademicRegistration)
                    .Include(x => x.KfStudentCourseEnrollments)
                        .ThenInclude(x => x.RetakeCaseCourse)
                            .ThenInclude(x => x!.StudentCourseResult)
                                .ThenInclude(x => x.StudentCourseEnrollment)
                                    .ThenInclude(x => x.ProgramCourse)
                    .Include(x => x.KfStudentCourseEnrollments)
                        .ThenInclude(x => x.RetakeCaseCourse)
                            .ThenInclude(x => x!.StudentCourseResult)
                                .ThenInclude(x => x.StudentCourseEnrollment)
                                    .ThenInclude(x => x.AcademicRegistration)
                    .FirstOrDefaultAsync(x => x.Id == academicRegistrationId);

                if (registration == null)
                    throw new CustomException("Academic registration not found.", StatusCodes.Status404NotFound);

                EnsureRegistrationUniversityAccess(registration, currentUser);

                if (registration.Student == null || registration.Student.StudentId != registration.StudentId)
                    throw new CustomException(
                        "The academic registration does not match its student record.",
                        StatusCodes.Status409Conflict);

                ValidateRegistrationCourseMappings(registration);

                var program = registration.Program;
                var application = registration.Application;
                if (program == null || application == null)
                    throw new CustomException(
                        "The academic registration is missing its program or application relationship.",
                        StatusCodes.Status409Conflict);

                if (!program.IsActive || program.AccreditationStatus != (byte)AccreditationStatusEnum.Accredited)
                    throw new CustomException(
                        "Progression is blocked because the program is inactive or is not currently accredited.",
                        StatusCodes.Status409Conflict);

                if (application.ApplicationStatus != (int)StudentApplicationStatus.Registered)
                    throw new CustomException(
                        "Progression requires an application that is currently Registered.",
                        StatusCodes.Status409Conflict);

                if (!registration.ProgramSemesterId.HasValue || registration.ProgramSemester == null ||
                    registration.ProgramSemester.ProgramSemesterId != registration.ProgramSemesterId.Value ||
                    registration.ProgramSemester.ProgramId != registration.ProgramId ||
                    registration.ProgramSemester.SemesterNo != registration.SemesterNo)
                {
                    throw new CustomException(
                        "Progression requires a valid persisted semester link on the previous registration. Legacy or ambiguous semester links must be reviewed before progression.",
                        StatusCodes.Status409Conflict);
                }

                var semesterResult = registration.KfStudentSemesterResult;
                if (semesterResult == null ||
                    (!string.Equals(semesterResult.SubmissionStatus, FinalizedSubmissionStatus, StringComparison.Ordinal) &&
                     !string.Equals(semesterResult.SubmissionStatus, "Submitted", StringComparison.Ordinal)) ||
                    !semesterResult.SubmittedBy.HasValue ||
                    !semesterResult.SubmittedOn.HasValue)
                {
                    throw new CustomException(
                        "The previous semester must have finalized results with submission audit details before progression.",
                        StatusCodes.Status409Conflict);
                }

                if (!semesterResult.SemesterGpa.HasValue || semesterResult.SemesterGpa.Value < 0m || semesterResult.SemesterGpa.Value > 4m)
                {
                    throw new CustomException("Enter the semester GPA (0.00 – 4.00).", StatusCodes.Status400BadRequest);
                }

                var enrollmentIds = registration.KfStudentCourseEnrollments
                    .Select(x => x.Id)
                    .ToHashSet();
                if (enrollmentIds.Count == 0)
                    throw new CustomException("Progression is blocked because the previous semester has no enrolled courses.");

                var savedCourseResults = await _context.KfStudentCourseResults
                    .AsNoTracking()
                    .Where(x => enrollmentIds.Contains(x.StudentCourseEnrollmentId))
                    .Select(x => new { x.Id, x.StudentCourseEnrollmentId, x.IsPassed })
                    .ToListAsync();

                var resultsByEnrollment = savedCourseResults
                    .GroupBy(x => x.StudentCourseEnrollmentId)
                    .ToDictionary(x => x.Key, x => x.ToList());

                if (savedCourseResults.Count != enrollmentIds.Count ||
                    enrollmentIds.Any(id => !resultsByEnrollment.TryGetValue(id, out var rows) ||
                                            rows.Count != 1 ||
                                            !rows[0].IsPassed.HasValue))
                {
                    throw new CustomException(
                        "Progression requires exactly one saved Pass or Fail outcome for every enrolled course.",
                        StatusCodes.Status409Conflict);
                }

                var failedCoreResults = registration.KfStudentCourseEnrollments
                    .Where(enrollment => resultsByEnrollment[enrollment.Id][0].IsPassed == false &&
                                         enrollment.ProgramCourse.CourseType == (byte)ProgramCourseTypeEnum.Core)
                    .Select(enrollment => new
                    {
                        ResultId = resultsByEnrollment[enrollment.Id][0].Id,
                        Enrollment = enrollment,
                        ProgramCourse = enrollment.ProgramCourse
                    })
                    .ToList();

                var retakeCourses = new List<RetakeCoursePlan>();
                var failingCoreCase = await _context.KfStudentFailingCoreCases
                    .Include(x => x.AcademicRegistration)
                    .Include(x => x.KfStudentFailingCoreCaseCourses)
                        .ThenInclude(x => x.StudentCourseResult)
                            .ThenInclude(x => x.StudentCourseEnrollment)
                                .ThenInclude(x => x.ProgramCourse)
                                    .ThenInclude(x => x.Course)
                    .FirstOrDefaultAsync(x => x.AcademicRegistrationId == registration.Id);

                if (failedCoreResults.Count > 0)
                {
                    if (failingCoreCase == null)
                        throw new CustomException(
                            "Progression is blocked because failed core courses have no persisted committee case.",
                            StatusCodes.Status409Conflict);
                    if (!string.Equals(failingCoreCase.Status, "Approved", StringComparison.Ordinal))
                        throw new CustomException(
                            failingCoreCase.Status == "Denied"
                                ? "Progression is blocked because the Committee denied continuation."
                                : "Progression is blocked while the failing-core case is pending Committee review.",
                            StatusCodes.Status409Conflict);
                    if (failingCoreCase.AcademicRegistration.ProgramId != registration.ProgramId ||
                        failingCoreCase.AcademicRegistration.StudentId != registration.StudentId ||
                        failingCoreCase.AcademicRegistration.ApplicationId != registration.ApplicationId ||
                        failingCoreCase.KfStudentFailingCoreCaseCourses.GroupBy(x => x.StudentCourseResultId).Any(x => x.Count() > 1) ||
                        failingCoreCase.KfStudentFailingCoreCaseCourses.Select(x => x.StudentCourseResultId).ToHashSet()
                            .SetEquals(failedCoreResults.Select(x => x.ResultId)) == false)
                    {
                        throw new CustomException(
                            "The approved failing-core case does not match the finalized failed core results.",
                            StatusCodes.Status409Conflict);
                    }

                    foreach (var failed in failedCoreResults)
                    {
                        var caseCourse = failingCoreCase.KfStudentFailingCoreCaseCourses.Single(x => x.StudentCourseResultId == failed.ResultId);
                        var sourceRegistration = caseCourse.StudentCourseResult.StudentCourseEnrollment.AcademicRegistration;
                        var sourceMapping = caseCourse.StudentCourseResult.StudentCourseEnrollment.ProgramCourse;
                        if (sourceRegistration.Id != registration.Id ||
                            sourceMapping.ProgramId != registration.ProgramId ||
                            sourceMapping.CourseType != (byte)ProgramCourseTypeEnum.Core ||
                            sourceMapping.ProgramCourseId != failed.ProgramCourse.ProgramCourseId ||
                            caseCourse.StudentCourseResult.IsPassed != false)
                        {
                            throw new CustomException(
                                "A failed core course in the approved case no longer matches its academic registration.",
                                StatusCodes.Status409Conflict);
                        }

                        retakeCourses.Add(new RetakeCoursePlan(sourceMapping, caseCourse.Id));
                    }
                }
                else if (failingCoreCase != null)
                {
                    throw new CustomException(
                        "A failing-core case exists but does not match a failed core outcome in the finalized results.",
                        StatusCodes.Status409Conflict);
                }

                if (program.NumberOfSemesters <= 0 || registration.SemesterNo < 1 ||
                    registration.SemesterNo >= program.NumberOfSemesters)
                {
                    throw new CustomException(
                        "The student is already at the final configured semester or the program semester configuration is invalid.",
                        StatusCodes.Status409Conflict);
                }

                var nextSemesterNo = registration.SemesterNo + 1;
                var nextSemesters = await _context.KfProgramSemesters
                    .AsNoTracking()
                    .Include(x => x.AcademicTerm)
                        .ThenInclude(x => x!.AcademicCalendar)
                    .Where(x => x.ProgramId == registration.ProgramId && x.SemesterNo == nextSemesterNo)
                    .Take(2)
                    .ToListAsync();

                if (nextSemesters.Count == 0)
                    throw new CustomException(
                        $"Program semester {nextSemesterNo} is not configured. No progression registration was created.",
                        StatusCodes.Status409Conflict);

                if (nextSemesters.Count > 1)
                    throw new CustomException(
                        $"Multiple persisted records match program semester {nextSemesterNo}. No progression registration was created.",
                        StatusCodes.Status409Conflict);

                var nextSemester = nextSemesters[0];
                if (nextSemester.ProgramId != program.ProgramId ||
                    nextSemester.SemesterNo != nextSemesterNo ||
                    !IsAcademicTermValidForProgram(nextSemester, program.UniversityId))
                {
                    throw new CustomException(
                        "The next program semester has an invalid university or academic-term relationship.",
                        StatusCodes.Status409Conflict);
                }

                var nextCourseMappings = await _context.KfProgramCourses
                    .AsNoTracking()
                    .Include(x => x.Course)
                    .Include(x => x.ProgramSemester)
                        .ThenInclude(x => x!.AcademicTerm)
                            .ThenInclude(x => x!.AcademicCalendar)
                    .Where(x => x.IsActive &&
                                x.Course.IsActive &&
                                ((x.ProgramId == program.ProgramId && x.SemesterNo == nextSemesterNo) ||
                                 x.ProgramSemesterId == nextSemester.ProgramSemesterId))
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.ProgramCourseId)
                    .ToListAsync();

                if (nextCourseMappings.Count == 0)
                    throw new CustomException(
                        "The next semester has no active course mappings. No progression registration was created.",
                        StatusCodes.Status409Conflict);

                if (nextCourseMappings.Any(x =>
                        x.Course == null ||
                        x.ProgramId != program.ProgramId ||
                        x.SemesterNo != nextSemesterNo ||
                        (x.ProgramSemesterId.HasValue &&
                         (x.ProgramSemesterId.Value != nextSemester.ProgramSemesterId ||
                          x.ProgramSemester == null ||
                          x.ProgramSemester.ProgramId != program.ProgramId ||
                          x.ProgramSemester.SemesterNo != nextSemesterNo ||
                          !IsAcademicTermValidForProgram(x.ProgramSemester, program.UniversityId)))))
                {
                    throw new CustomException(
                        "One or more active course mappings conflict with the next program semester. No progression registration was created.",
                        StatusCodes.Status409Conflict);
                }

                if (nextCourseMappings.GroupBy(x => x.CourseId).Any(x => x.Count() > 1))
                    throw new CustomException(
                        "The next semester contains duplicate active course mappings. No progression registration was created.",
                        StatusCodes.Status409Conflict);

                var existingNextRegistrations = await _context.KfStudentAcademicRegistrations
                    .AsNoTracking()
                    .Where(x => x.ApplicationId == application.ApplicationId &&
                                (x.ProgramSemesterId == nextSemester.ProgramSemesterId ||
                                 x.SemesterNo == nextSemesterNo))
                    .Select(x => new { x.Id, x.ProgramId, x.SemesterNo, x.ProgramSemesterId })
                    .Take(2)
                    .ToListAsync();

                if (existingNextRegistrations.Count > 0)
                    throw new CustomException(
                        "An academic registration already exists for this application and next semester. Refresh Academic History before continuing.",
                        StatusCodes.Status409Conflict);

                var now = DateTime.UtcNow;
                var warnings = new List<AcademicProgressionWarningDto>();
                var centralWindowOpen = await _context.KfUniversityRegistrationWindows
                    .AsNoTracking()
                    .AnyAsync(x => x.UniversityId == program.UniversityId &&
                                   x.IsOpen &&
                                   x.RegistrationFrom.HasValue &&
                                   x.RegistrationFrom.Value <= now &&
                                   x.RegistrationTo >= now);
                if (!centralWindowOpen)
                {
                    warnings.Add(new AcademicProgressionWarningDto
                    {
                        Code = CentralRegistrationRestrictionCode,
                        Message = "The university central registration window is not currently open."
                    });
                }

                var programWindowOpen = await _context.KfProgramRegistrationWindows
                    .AsNoTracking()
                    .AnyAsync(x => x.ProgramId == program.ProgramId &&
                                   x.SemesterNo == nextSemesterNo &&
                                   x.RegistrationFrom <= now &&
                                   x.RegistrationTo >= now);
                if (!programWindowOpen)
                {
                    warnings.Add(new AcademicProgressionWarningDto
                    {
                        Code = ProgramRegistrationRestrictionCode,
                        Message = $"No program registration window for Semester {nextSemesterNo} is currently open."
                    });
                }

                var confirmedCodes = (dto.ConfirmedRegistrationRestrictionCodes ?? new List<string>())
                    .Distinct(StringComparer.Ordinal)
                    .ToHashSet(StringComparer.Ordinal);
                var recognizedCodes = new HashSet<string>(StringComparer.Ordinal)
                {
                    CentralRegistrationRestrictionCode,
                    ProgramRegistrationRestrictionCode
                };
                if (confirmedCodes.Any(code => !recognizedCodes.Contains(code)))
                    throw new CustomException("The progression request contains an unknown registration restriction confirmation.");

                var unconfirmedWarnings = warnings.Where(x => !confirmedCodes.Contains(x.Code)).ToList();
                if (unconfirmedWarnings.Count > 0)
                {
                    await TryRollbackProgressionAsync(transaction);
                    return new AcademicProgressionResponseDto
                    {
                        ConfirmationRequired = true,
                        Warnings = warnings,
                        ApplicationId = application.ApplicationId,
                        ProgramId = program.ProgramId,
                        CurrentSemesterNo = registration.SemesterNo,
                        NextSemesterNo = nextSemester.SemesterNo,
                        NextProgramSemesterId = nextSemester.ProgramSemesterId,
                        NextSemesterName = nextSemester.SemesterName,
                        NextTermName = nextSemester.AcademicTerm?.TermName,
                        AcademicYearName = nextSemester.AcademicTerm?.AcademicCalendar?.AcademicYearName,
                        SemesterStartDate = nextSemester.StartDate,
                        SemesterEndDate = nextSemester.EndDate
                    };
                }

                var nextRegistration = new KfStudentAcademicRegistration
                {
                    StudentId = registration.StudentId,
                    ApplicationId = application.ApplicationId,
                    ProgramId = program.ProgramId,
                    SemesterNo = nextSemesterNo,
                    ProgramSemesterId = nextSemester.ProgramSemesterId,
                    RegistrationDate = now,
                    CreatedBy = currentUser.LoginId,
                    CreatedOn = now
                };

                _context.KfStudentAcademicRegistrations.Add(nextRegistration);
                await _context.SaveChangesAsync();

                var enrollmentSummary = await EnrollActiveCoursesAsync(
                    nextRegistration.Id,
                    nextRegistration.ProgramId,
                    nextRegistration.SemesterNo,
                    nextSemester.ProgramSemesterId,
                    currentUser.LoginId,
                    retakeCourses);

                await transaction.CommitAsync();

                return new AcademicProgressionResponseDto
                {
                    ConfirmationRequired = false,
                    Warnings = warnings,
                    AcademicRegistrationId = nextRegistration.Id,
                    ApplicationId = application.ApplicationId,
                    ProgramId = program.ProgramId,
                    CurrentSemesterNo = registration.SemesterNo,
                    NextSemesterNo = nextRegistration.SemesterNo,
                    NextProgramSemesterId = nextSemester.ProgramSemesterId,
                    NextSemesterName = nextSemester.SemesterName,
                    NextTermName = nextSemester.AcademicTerm?.TermName,
                    AcademicYearName = nextSemester.AcademicTerm?.AcademicCalendar?.AcademicYearName,
                    SemesterStartDate = nextSemester.StartDate,
                    SemesterEndDate = nextSemester.EndDate,
                    EnrollmentSummary = enrollmentSummary
                };
            }
            catch (Exception ex) when (IsSqlServerDeadlock(ex))
            {
                await TryRollbackProgressionAsync(transaction);
                throw new CustomException(
                    "Another progression request was processed concurrently. Refresh Academic History before retrying.",
                    StatusCodes.Status409Conflict);
            }
            catch (DbUpdateException ex) when (IsCourseEnrollmentDuplicateConflict(ex))
            {
                await TryRollbackProgressionAsync(transaction);
                throw new CustomException(
                    "A course enrollment already exists for this progression registration. No partial progression was committed.",
                    StatusCodes.Status409Conflict);
            }
            catch
            {
                await TryRollbackProgressionAsync(transaction);
                throw;
            }
        }

        private static bool IsSqlServerDeadlock(Exception exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is SqlException sqlException && sqlException.Number == 1205)
                    return true;
            }

            return false;
        }

        private static async Task TryRollbackProgressionAsync(IDbContextTransaction transaction)
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                try
                {
                    FileLogger.LogError(
                        "N/A",
                        rollbackException,
                        "StudentAcademicRegistrationService.ProgressToNextSemesterAsync rollback");
                }
                catch
                {
                    // Preserve the original operation outcome if rollback logging fails.
                }
            }
        }

        private static async Task TryRollbackFinalizationAsync(IDbContextTransaction transaction)
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch (Exception rollbackException)
            {
                try
                {
                    FileLogger.LogError(
                        "N/A",
                        rollbackException,
                        "StudentAcademicRegistrationService.FinalizeResultsAsync rollback");
                }
                catch
                {
                    // Preserve the original operation exception if rollback logging also fails.
                }
            }
        }

        private async Task<KfStudentAcademicRegistration?> GetRegistrationWithResultsAsync(
            long academicRegistrationId,
            bool asNoTracking)
        {
            IQueryable<KfStudentAcademicRegistration> query = _context.KfStudentAcademicRegistrations;
            if (asNoTracking)
                query = query.AsNoTracking();

            return await query
                .Include(x => x.Program)
                .Include(x => x.Application)
                .Include(x => x.ProgramSemester)
                    .ThenInclude(x => x!.AcademicTerm)
                        .ThenInclude(x => x!.AcademicCalendar)
                .Include(x => x.KfStudentSemesterResult)
                .Include(x => x.KfStudentCourseEnrollments)
                    .ThenInclude(x => x.ProgramCourse)
                        .ThenInclude(x => x.Course)
                .Include(x => x.KfStudentCourseEnrollments)
                    .ThenInclude(x => x.ProgramCourse)
                        .ThenInclude(x => x.ProgramSemester)
                            .ThenInclude(x => x!.AcademicTerm)
                                .ThenInclude(x => x!.AcademicCalendar)
                .Include(x => x.KfStudentCourseEnrollments)
                    .ThenInclude(x => x.KfStudentCourseResult)
                .Include(x => x.KfStudentCourseEnrollments)
                    .ThenInclude(x => x.RetakeCaseCourse)
                        .ThenInclude(x => x!.FailingCoreCase)
                            .ThenInclude(x => x.AcademicRegistration)
                .Include(x => x.KfStudentCourseEnrollments)
                    .ThenInclude(x => x.RetakeCaseCourse)
                        .ThenInclude(x => x!.StudentCourseResult)
                            .ThenInclude(x => x.StudentCourseEnrollment)
                                .ThenInclude(x => x.ProgramCourse)
                .Include(x => x.KfStudentCourseEnrollments)
                    .ThenInclude(x => x.RetakeCaseCourse)
                        .ThenInclude(x => x!.StudentCourseResult)
                            .ThenInclude(x => x.StudentCourseEnrollment)
                                .ThenInclude(x => x.AcademicRegistration)
                .FirstOrDefaultAsync(x => x.Id == academicRegistrationId);
        }

        private static bool TimestampsMatch(DateTime? expected, DateTime? actual)
        {
            if (!expected.HasValue && !actual.HasValue) return true;
            if (!expected.HasValue || !actual.HasValue) return false;

            var expUtc = expected.Value.Kind == DateTimeKind.Local
                ? expected.Value.ToUniversalTime()
                : DateTime.SpecifyKind(expected.Value, DateTimeKind.Utc);

            var actUtc = actual.Value.Kind == DateTimeKind.Local
                ? actual.Value.ToUniversalTime()
                : DateTime.SpecifyKind(actual.Value, DateTimeKind.Utc);

            if (expUtc == actUtc) return true;

            return Math.Abs((expUtc - actUtc).TotalMilliseconds) < 1.0;
        }

        private static void EnsureAcademicRecordReadAccess(LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.University && currentUser.StaffType != StaffType.Ngo && !currentUser.IsSuperAdmin)
                throw new UnauthorizedAccessException();

            if (currentUser.StaffType == StaffType.University && !currentUser.UniversityIds.Any())
                throw new UnauthorizedAccessException("User is not associated with a university.");
        }

        private static void EnsureRegistrationReadAccess(
            KfStudentAcademicRegistration registration,
            LoggedInUserDto currentUser)
        {
            if (registration.Program == null || registration.Application == null)
            {
                throw new CustomException(
                    "The academic registration is missing its program or application relationship.",
                    StatusCodes.Status409Conflict);
            }

            if (currentUser.StaffType == StaffType.University && !currentUser.UniversityIds.Contains(registration.Program.UniversityId))
                throw new UnauthorizedAccessException();

            if (registration.Application.ProgramId != registration.ProgramId ||
                registration.Application.StudentId != registration.StudentId)
                throw new CustomException(
                    "The academic registration does not match the application's student and program.",
                    StatusCodes.Status409Conflict);
        }

        private static void EnsureUniversityAccess(LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.University)
                throw new UnauthorizedAccessException();

            if (!currentUser.UniversityIds.Any())
                throw new UnauthorizedAccessException("User is not associated with a university.");
        }

        private static bool IsAcademicTermValidForProgram(
            KfProgramSemester semester,
            long universityId)
        {
            if (!semester.AcademicTermId.HasValue)
                return true;

            return semester.AcademicTerm != null &&
                   semester.AcademicTerm.AcademicCalendar != null &&
                   semester.AcademicTerm.AcademicCalendar.UniversityId == universityId;
        }

        private static void EnsureRegistrationUniversityAccess(
            KfStudentAcademicRegistration registration,
            LoggedInUserDto currentUser)
        {
            if (registration.Program == null || registration.Application == null)
            {
                throw new CustomException(
                    "The academic registration is missing its program or application relationship.",
                    StatusCodes.Status409Conflict);
            }

            if (!currentUser.UniversityIds.Contains(registration.Program.UniversityId))
                throw new UnauthorizedAccessException();

            if (registration.Application.ProgramId != registration.ProgramId ||
                registration.Application.StudentId != registration.StudentId)
                throw new CustomException(
                    "The academic registration does not match the application's student and program.",
                    StatusCodes.Status409Conflict);
        }

        private static void ValidateRegistrationCourseMappings(KfStudentAcademicRegistration registration)
        {
            if (registration.ProgramSemesterId.HasValue &&
                (registration.ProgramSemester == null ||
                 registration.ProgramSemester.ProgramId != registration.ProgramId ||
                 registration.ProgramSemester.SemesterNo != registration.SemesterNo ||
                 !IsAcademicTermValidForProgram(registration.ProgramSemester, registration.Program.UniversityId)))
            {
                throw new CustomException(
                    "The academic registration has an invalid program-semester mapping.",
                    StatusCodes.Status409Conflict);
            }

            foreach (var enrollment in registration.KfStudentCourseEnrollments)
            {
                var programCourse = enrollment.ProgramCourse;
                if (programCourse == null ||
                    programCourse.ProgramId != registration.ProgramId ||
                    programCourse.Course == null)
                {
                    throw new CustomException(
                        "An enrolled course does not have a valid course mapping for the registered program semester.",
                        StatusCodes.Status409Conflict);
                }

                if (enrollment.RetakeCaseCourseId.HasValue)
                {
                    var retakeCaseCourse = enrollment.RetakeCaseCourse;
                    var failingCoreCase = retakeCaseCourse?.FailingCoreCase;
                    var sourceRegistration = retakeCaseCourse?.FailingCoreCase?.AcademicRegistration;
                    var sourceResult = retakeCaseCourse?.StudentCourseResult;
                    var sourceEnrollment = sourceResult?.StudentCourseEnrollment;
                    if (retakeCaseCourse == null ||
                        failingCoreCase == null ||
                        retakeCaseCourse.Id != enrollment.RetakeCaseCourseId.Value ||
                        !string.Equals(failingCoreCase.Status, "Approved", StringComparison.Ordinal) ||
                        sourceRegistration == null ||
                        sourceRegistration.Id != failingCoreCase.AcademicRegistrationId ||
                        sourceRegistration.StudentId != registration.StudentId ||
                        sourceRegistration.ApplicationId != registration.ApplicationId ||
                        sourceRegistration.ProgramId != registration.ProgramId ||
                        sourceRegistration.SemesterNo >= registration.SemesterNo ||
                        sourceResult?.IsPassed != false ||
                        sourceEnrollment?.ProgramCourseId != programCourse.ProgramCourseId ||
                        programCourse.CourseType != (byte)ProgramCourseTypeEnum.Core ||
                        programCourse.SemesterNo > sourceRegistration.SemesterNo)
                    {
                        throw new CustomException(
                            "An enrolled retake does not match an approved failed-core case for this student and program.",
                            StatusCodes.Status409Conflict);
                    }
                }
                else if (programCourse.SemesterNo != registration.SemesterNo)
                {
                    throw new CustomException(
                        "An enrolled course does not have a valid course mapping for the registered program semester.",
                        StatusCodes.Status409Conflict);
                }

                if (programCourse.ProgramSemesterId.HasValue &&
                    (programCourse.ProgramSemester == null ||
                     programCourse.ProgramSemester.ProgramId != registration.ProgramId ||
                     programCourse.ProgramSemester.SemesterNo != programCourse.SemesterNo ||
                     !IsAcademicTermValidForProgram(programCourse.ProgramSemester, registration.Program.UniversityId) ||
                     (!enrollment.RetakeCaseCourseId.HasValue && registration.ProgramSemesterId.HasValue &&
                      programCourse.ProgramSemesterId != registration.ProgramSemesterId)))
                {
                    throw new CustomException(
                        "An enrolled course has a conflicting program-semester mapping.",
                        StatusCodes.Status409Conflict);
                }
            }

            if (registration.KfStudentCourseEnrollments
                .GroupBy(x => x.ProgramCourse.CourseId)
                .Any(x => x.Count() > 1 &&
                          !(x.Count() == 2 && x.Count(y => y.RetakeCaseCourseId.HasValue) == 1)))
            {
                throw new CustomException("The academic registration contains duplicate mappings for a course.");
            }
        }

        private static AcademicRegistrationResultsDto MapAcademicRegistrationResults(
            KfStudentAcademicRegistration registration)
        {
            return new AcademicRegistrationResultsDto
            {
                AcademicRegistrationId = registration.Id,
                ApplicationId = registration.ApplicationId,
                ProgramId = registration.ProgramId,
                SemesterNo = registration.SemesterNo,
                ProgramSemesterId = registration.ProgramSemesterId,
                SemesterGpa = registration.KfStudentSemesterResult?.SemesterGpa,
                InternalNote = registration.KfStudentSemesterResult?.InternalNote,
                ExpectedUpdatedOn = registration.KfStudentSemesterResult?.UpdatedOn,
                SubmissionStatus = registration.KfStudentSemesterResult?.SubmissionStatus,
                SubmittedOn = registration.KfStudentSemesterResult?.SubmittedOn,
                Courses = registration.KfStudentCourseEnrollments
                    .OrderBy(x => x.ProgramCourse.DisplayOrder)
                    .ThenBy(x => x.ProgramCourse.ProgramCourseId)
                    .Select(x => new CourseResultEntryDto
                    {
                        StudentCourseEnrollmentId = x.Id,
                        ProgramCourseId = x.ProgramCourseId,
                        CourseId = x.ProgramCourse.CourseId,
                        CourseCode = x.ProgramCourse.Course.CourseCode,
                        CourseName = x.ProgramCourse.Course.CourseNameEn,
                        CourseType = x.ProgramCourse.CourseType,
                        Credits = x.ProgramCourse.Credits,
                        IsRetake = x.RetakeCaseCourseId.HasValue,
                        IsPassed = x.KfStudentCourseResult?.IsPassed,
                        Grade = x.KfStudentCourseResult?.Grade,
                        Remarks = x.KfStudentCourseResult?.Remarks
                    })
                    .ToList()
            };
        }

        private static bool IsAcademicResultDuplicateConflict(DbUpdateException exception)
        {
            var expectedConstraints = new[]
            {
                "UQ_kf_student_course_results_Enrollment",
                "UQ_kf_student_semester_results_Registration"
            };

            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is SqlException sqlException &&
                    sqlException.Errors.Cast<SqlError>().Any(error =>
                        (error.Number is 2601 or 2627) &&
                        expectedConstraints.Any(name => error.Message.Contains(name, StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<CourseEnrollmentSummaryDto> EnrollActiveCoursesAsync(
            long academicRegistrationId,
            long programId,
            int semesterNo,
            long programSemesterId,
            long actorId,
            IReadOnlyCollection<RetakeCoursePlan>? retakeCourses = null)
        {
            var courseMappings = await _context.KfProgramCourses
                .AsNoTracking()
                .Include(x => x.Course)
                .Where(x => x.IsActive &&
                            x.Course.IsActive &&
                            ((x.ProgramId == programId && x.SemesterNo == semesterNo) ||
                             x.ProgramSemesterId == programSemesterId))
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.ProgramCourseId)
                .ToListAsync();

            if (courseMappings.Count == 0)
            {
                throw new CustomException(
                    "No active courses are configured for the selected program semester. No registration changes were committed.");
            }

            if (courseMappings.Any(x =>
                    x.ProgramId != programId ||
                    x.SemesterNo != semesterNo ||
                    (x.ProgramSemesterId.HasValue && x.ProgramSemesterId.Value != programSemesterId)))
            {
                throw new CustomException(
                    "One or more active course mappings conflict with the selected program semester. No courses were enrolled.");
            }

            if (courseMappings.GroupBy(x => x.CourseId).Any(x => x.Count() > 1))
            {
                throw new CustomException(
                    "The selected program semester contains duplicate active course mappings. No courses were enrolled.");
            }

            var enrollmentPlans = courseMappings
                .Select(x => (ProgramCourse: x, RetakeCaseCourseId: (long?)null))
                .Concat((retakeCourses ?? Array.Empty<RetakeCoursePlan>())
                    .Select(x => (ProgramCourse: x.ProgramCourse, RetakeCaseCourseId: (long?)x.FailingCoreCaseCourseId)))
                .ToList();

            if (enrollmentPlans.GroupBy(x => x.ProgramCourse.ProgramCourseId).Any(x => x.Count() > 1))
                throw new CustomException("The selected semester contains duplicate course mapping IDs.");

            var existingProgramCourseIds = await _context.KfStudentCourseEnrollments
                .AsNoTracking()
                .Where(x => x.AcademicRegistrationId == academicRegistrationId)
                .Select(x => x.ProgramCourseId)
                .ToHashSetAsync();

            var coursesToEnroll = enrollmentPlans
                .Where(x => !existingProgramCourseIds.Contains(x.ProgramCourse.ProgramCourseId))
                .ToList();

            if (coursesToEnroll.Count > 0)
            {
                var now = DateTime.UtcNow;
                _context.KfStudentCourseEnrollments.AddRange(coursesToEnroll.Select(x => new KfStudentCourseEnrollment
                {
                    AcademicRegistrationId = academicRegistrationId,
                    ProgramCourseId = x.ProgramCourse.ProgramCourseId,
                    RetakeCaseCourseId = x.RetakeCaseCourseId,
                    CreatedBy = actorId,
                    CreatedOn = now
                }));
            }

            await _context.SaveChangesAsync();

            return new CourseEnrollmentSummaryDto
            {
                NewlyCreatedCount = coursesToEnroll.Count,
                AlreadyExistingCount = enrollmentPlans.Count - coursesToEnroll.Count
            };
        }

        private sealed record RetakeCoursePlan(KfProgramCourse ProgramCourse, long FailingCoreCaseCourseId);

        private static bool IsCourseEnrollmentDuplicateConflict(DbUpdateException exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is SqlException sqlException &&
                    sqlException.Errors.Cast<SqlError>().Any(error =>
                        (error.Number is 2601 or 2627) &&
                        error.Message.Contains(
                            "UQ_kf_student_course_enrollments_Registration_Course",
                            StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsFailingCoreCaseDuplicateConflict(DbUpdateException exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is SqlException sqlException &&
                    sqlException.Errors.Cast<SqlError>().Any(error =>
                        (error.Number is 2601 or 2627) &&
                        error.Message.Contains(
                            "UQ_kf_student_failing_core_cases_Registration",
                            StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAcademicRegistrationDuplicateConflict(DbUpdateException exception)
        {
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                if (current is SqlException sqlException &&
                    sqlException.Errors.Cast<SqlError>().Any(error =>
                        (error.Number is 2601 or 2627) &&
                        (error.Message.Contains("kf_student_academic_registrations", StringComparison.OrdinalIgnoreCase) ||
                         error.Message.Contains("UQ_kf_student_academic_registrations", StringComparison.OrdinalIgnoreCase))))
                {
                    return true;
                }
            }

            return false;
        }



        public async Task<PagedResultDto<AcademicRegistrationDto>> SearchAsync(AcademicRegistrationFilterDto filter, LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.University)
                throw new UnauthorizedAccessException();

            if (!currentUser.UniversityIds.Any())
                throw new UnauthorizedAccessException("User is not associated with a university.");

            var query = _context.KfStudentProgramApplications
                .AsNoTracking()
                .Where(x =>
                    currentUser.UniversityIds.Contains(x.Program.UniversityId) &&
                    x.ApplicationStatus == (int)StudentApplicationStatus.Sponsored);

            if (filter.FacultyId.HasValue)
                query = query.Where(x => x.Program.FacultyId == filter.FacultyId);

            if (filter.ProgramId.HasValue)
                query = query.Where(x => x.ProgramId == filter.ProgramId);

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim();

                query = query.Where(x =>
                    x.Student.FirstName.Contains(search) ||
                    x.Student.LastName.Contains(search) ||
                    x.Student.StudentCode.Contains(search) ||
                    x.Program.ProgramName.Contains(search) ||
                    x.Program.ProgramCode.Contains(search));
            }

            var totalCount = await query.CountAsync();

            query = query.OrderByDescending(x => x.SubmittedDate ?? x.AppliedDate);

            if (filter.PageSize > 0)
            {
                query = query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize);
            }

            var items = await query
                .Select(x => new AcademicRegistrationDto
                {
                    StudentId = x.StudentId,
                    StudentCode = x.Student.StudentCode,

                    StudentName = string.Join(" ",
                        new[]
                        {
                    x.Student.FirstName,
                    x.Student.SecondName,
                    x.Student.ThirdName,
                    x.Student.LastName
                        }),

                    PhotoPath = x.Student.PhotoPath,

                    ApplicationId = x.ApplicationId,

                    ProgramId = x.ProgramId,
                    ProgramName = x.Program.ProgramName,

                    FacultyId = x.Program.FacultyId,
                    FacultyName = x.Program.Faculty.FacultyName,

                    UniversityId = x.Program.UniversityId,
                    UniversityName = x.Program.University.UniversityName,

                    SemesterNo = 1,
                    RegistrationDate = DateTime.UtcNow,

                    ApplicationStatusId = x.ApplicationStatus,
                    ApplicationStatusName = ((StudentApplicationStatus)x.ApplicationStatus).ToString()
                })
                .ToListAsync();

            return new PagedResultDto<AcademicRegistrationDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }






        private StudentHistoryTypeEnum GetHistoryType(StudentApplicationStatus status)
        {
            return status switch
            {
                StudentApplicationStatus.Registered => StudentHistoryTypeEnum.StudentRegistered,
                _ => StudentHistoryTypeEnum.ApplicationUpdated
            };
        }
       
        private string GetHistoryTitle(StudentApplicationStatus status)
        {
            return status switch
            {
                StudentApplicationStatus.Registered => "Student Registered",
                _ => "Application Updated"
            };
        }

        private string GetHistoryDescription(StudentApplicationStatus status)
        {
            return status switch
            {
                StudentApplicationStatus.Registered =>
                    "Student registered successfully.",

                _ => "Application status updated."
            };
        }

        private Task AddHistoryAsync(long studentId, long applicationId, string title,
            string description, StudentHistoryTypeEnum historyType, long userId)
        {
            _context.KfStudentHistories.Add(new KfStudentHistory
            {
                StudentId = studentId,
                ApplicationId = applicationId,
                Title = title,
                Description = description,
                HistoryType = (int)historyType,
                CreatedBy = userId,
                CreatedDate = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }




    }

}
