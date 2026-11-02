using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Ngo.FailingCoreCases;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.Ngo;

namespace ScholarshipManagementAPI.Services.Implementation.Ngo;

public sealed class FailingCoreCaseService : IFailingCoreCaseService
{
    private const string Pending = "Pending";
    private const string Approved = "Approved";
    private const string Denied = "Denied";
    private readonly AppDbContext _context;

    public FailingCoreCaseService(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<FailingCoreCaseDto>> GetCasesAsync(string? status, LoggedInUserDto currentUser)
    {
        EnsureCommitteeAccess(currentUser);
        var normalizedStatus = string.IsNullOrWhiteSpace(status) || string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : NormalizeDecisionStatus(status);

        var query = CaseDetailsQuery();
        if (normalizedStatus != null)
            query = query.Where(x => x.Status == normalizedStatus);

        var cases = await query
            .OrderByDescending(x => x.SubmittedOn)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        return cases.Select(Map).ToList();
    }

    public async Task<FailingCoreCaseDetailDto?> GetByIdAsync(long id, LoggedInUserDto currentUser)
    {
        EnsureCommitteeAccess(currentUser);
        var entity = await CaseDetailQuery().FirstOrDefaultAsync(x => x.Id == id);
        return entity == null ? null : MapDetail(entity);
    }

    public async Task<FailingCoreCaseDetailDto> RecordDecisionAsync(
        long id,
        FailingCoreCaseDecisionRequestDto request,
        LoggedInUserDto currentUser)
    {
        EnsureCommitteeAccess(currentUser);
        var decision = NormalizeDecisionStatus(request.Status);
        var comment = request.Comment?.Trim();
        if (string.IsNullOrWhiteSpace(comment))
            throw new CustomException("A committee comment is required before recording a decision.");
        if (comment.Length > 2000)
            throw new CustomException("The committee comment cannot exceed 2,000 characters.");
        if (request.ExpectedRowVersion == null || request.ExpectedRowVersion.Length == 0)
            throw new CustomException("Reload this case before recording a decision.", StatusCodes.Status409Conflict);

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var entity = await CaseDetailQuery(asTracking: true).FirstOrDefaultAsync(x => x.Id == id);
            if (entity == null)
                throw new CustomException("Failing-core case not found.", StatusCodes.Status404NotFound);
            if (!string.Equals(entity.Status, Pending, StringComparison.Ordinal))
                throw new CustomException("A decision has already been recorded for this case.", StatusCodes.Status409Conflict);
            if (!entity.RowVersion.AsSpan().SequenceEqual(request.ExpectedRowVersion))
                throw new CustomException("This case changed after it was loaded. Reload it before recording a decision.", StatusCodes.Status409Conflict);

            _context.Entry(entity).Property(x => x.RowVersion).OriginalValue = request.ExpectedRowVersion;
            var now = DateTime.UtcNow;
            entity.Status = decision;
            entity.DecisionComment = comment;
            entity.DecisionBy = currentUser.LoginId;
            entity.DecisionOn = now;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return MapDetail(entity);
        }
        catch (DbUpdateConcurrencyException)
        {
            await TryRollbackAsync(transaction);
            throw new CustomException("Another committee decision was recorded concurrently. Reload the case.", StatusCodes.Status409Conflict);
        }
        catch
        {
            await TryRollbackAsync(transaction);
            throw;
        }
    }



    private IQueryable<KfStudentFailingCoreCase> CaseDetailsQuery(bool asTracking = false)
    {
        IQueryable<KfStudentFailingCoreCase> query = _context.KfStudentFailingCoreCases;
        if (!asTracking)
            query = query.AsNoTracking();

        return query
            .Include(x => x.AcademicRegistration).ThenInclude(x => x.Student)
            .Include(x => x.AcademicRegistration).ThenInclude(x => x.Program).ThenInclude(x => x.University)
            .Include(x => x.AcademicRegistration).ThenInclude(x => x.ProgramSemester)
                .ThenInclude(x => x!.AcademicTerm).ThenInclude(x => x!.AcademicCalendar)
            .Include(x => x.KfStudentFailingCoreCaseCourses).ThenInclude(x => x.StudentCourseResult)
                .ThenInclude(x => x.StudentCourseEnrollment).ThenInclude(x => x.ProgramCourse).ThenInclude(x => x.Course);
    }

    private IQueryable<KfStudentFailingCoreCase> CaseDetailQuery(bool asTracking = false) =>
        CaseDetailsQuery(asTracking)
            .Include(x => x.AcademicRegistration).ThenInclude(x => x.KfStudentSemesterResult)
            .Include(x => x.AcademicRegistration).ThenInclude(x => x.KfStudentCourseEnrollments)
                .ThenInclude(x => x.ProgramCourse).ThenInclude(x => x.Course)
            .Include(x => x.AcademicRegistration).ThenInclude(x => x.KfStudentCourseEnrollments)
                .ThenInclude(x => x.KfStudentCourseResult)
            .AsSplitQuery();

    private static FailingCoreCaseDto Map(KfStudentFailingCoreCase entity)
    {
        var registration = entity.AcademicRegistration;
        return new FailingCoreCaseDto
        {
            Id = entity.Id,
            AcademicRegistrationId = registration.Id,
            ApplicationId = registration.ApplicationId,
            StudentId = registration.StudentId,
            StudentCode = registration.Student.StudentCode,
            StudentName = string.Join(" ", new[]
            {
                registration.Student.FirstName,
                registration.Student.SecondName,
                registration.Student.ThirdName,
                registration.Student.LastName
            }.Where(x => !string.IsNullOrWhiteSpace(x))),
            UniversityId = registration.Program.UniversityId,
            UniversityName = registration.Program.University.UniversityName,
            ProgramId = registration.ProgramId,
            ProgramName = registration.Program.ProgramName,
            SemesterNo = registration.SemesterNo,
            SemesterName = registration.ProgramSemester?.SemesterName,
            TermName = registration.ProgramSemester?.AcademicTerm?.TermName,
            AcademicYearName = registration.ProgramSemester?.AcademicTerm?.AcademicCalendar?.AcademicYearName,
            SemesterStartDate = registration.ProgramSemester?.StartDate,
            SemesterEndDate = registration.ProgramSemester?.EndDate,
            SubmittedOn = entity.SubmittedOn,
            Status = entity.Status,
            DecisionComment = entity.DecisionComment,
            DecisionOn = entity.DecisionOn,
            RowVersion = entity.RowVersion,
            FailedCoreCourses = entity.KfStudentFailingCoreCaseCourses
                .OrderBy(x => x.Id)
                .Select(x => new FailingCoreCaseCourseDto
                {
                    StudentCourseResultId = x.StudentCourseResultId,
                    ProgramCourseId = x.StudentCourseResult.StudentCourseEnrollment.ProgramCourseId,
                    CourseId = x.StudentCourseResult.StudentCourseEnrollment.ProgramCourse.CourseId,
                    CourseCode = x.StudentCourseResult.StudentCourseEnrollment.ProgramCourse.Course.CourseCode,
                    CourseName = x.StudentCourseResult.StudentCourseEnrollment.ProgramCourse.Course.CourseNameEn,
                    Credits = x.StudentCourseResult.StudentCourseEnrollment.ProgramCourse.Credits
                })
                .ToList()
        };
    }

    private static FailingCoreCaseDetailDto MapDetail(KfStudentFailingCoreCase entity)
    {
        var summary = Map(entity);
        var detail = new FailingCoreCaseDetailDto
        {
            Id = summary.Id,
            AcademicRegistrationId = summary.AcademicRegistrationId,
            ApplicationId = summary.ApplicationId,
            StudentId = summary.StudentId,
            StudentCode = summary.StudentCode,
            StudentName = summary.StudentName,
            UniversityId = summary.UniversityId,
            UniversityName = summary.UniversityName,
            ProgramId = summary.ProgramId,
            ProgramName = summary.ProgramName,
            SemesterNo = summary.SemesterNo,
            SemesterName = summary.SemesterName,
            TermName = summary.TermName,
            AcademicYearName = summary.AcademicYearName,
            SemesterStartDate = summary.SemesterStartDate,
            SemesterEndDate = summary.SemesterEndDate,
            SubmittedOn = summary.SubmittedOn,
            Status = summary.Status,
            DecisionComment = summary.DecisionComment,
            DecisionOn = summary.DecisionOn,
            RowVersion = summary.RowVersion,
            FailedCoreCourses = summary.FailedCoreCourses,
            SemesterGpa = entity.AcademicRegistration.KfStudentSemesterResult?.SemesterGpa,
            SemesterCourses = entity.AcademicRegistration.KfStudentCourseEnrollments
                .OrderBy(x => x.ProgramCourse.DisplayOrder)
                .ThenBy(x => x.ProgramCourse.ProgramCourseId)
                .Select(x => new FailingCoreCaseSemesterCourseDto
                {
                    CourseCode = x.ProgramCourse.Course.CourseCode,
                    CourseName = x.ProgramCourse.Course.CourseNameEn,
                    CourseType = x.ProgramCourse.CourseType switch
                    {
                        (byte)ProgramCourseTypeEnum.Core => "Core",
                        (byte)ProgramCourseTypeEnum.Elective => "Elective",
                        _ => "Unknown"
                    },
                    Credits = x.ProgramCourse.Credits,
                    IsPassed = x.KfStudentCourseResult?.IsPassed,
                    IsRetake = x.RetakeCaseCourseId.HasValue
                })
                .ToList()
        };
        return detail;
    }

    private static string NormalizeDecisionStatus(string? status)
    {
        if (string.Equals(status?.Trim(), Approved, StringComparison.OrdinalIgnoreCase)) return Approved;
        if (string.Equals(status?.Trim(), Denied, StringComparison.OrdinalIgnoreCase)) return Denied;
        throw new CustomException("Decision status must be Approved or Denied.");
    }

    private static void EnsureCommitteeAccess(LoggedInUserDto currentUser)
    {
        // The existing Committee portal is served by the NGO staff module.
        if (currentUser.StaffType != StaffType.Ngo)
            throw new UnauthorizedAccessException("Only authorized Committee users can access failing-core cases.");
    }

    private static async Task TryRollbackAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch (Exception rollbackException)
        {
            try
            {
                FileLogger.LogError("N/A", rollbackException, "FailingCoreCaseService.RecordDecisionAsync rollback");
            }
            catch
            {
                // Preserve the original operation error if rollback logging also fails.
            }
        }
    }
}
