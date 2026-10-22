using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.University.AcademicConfig;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.University;

namespace ScholarshipManagementAPI.Services.Implementation.University
{
    public class UniversityAcademicCalendarService : IUniversityAcademicCalendarService
    {
        private static readonly HashSet<string> AllowedNamingStyles = new(StringComparer.OrdinalIgnoreCase)
        {
            "Term", "Semester", "Seasonal", "Custom"
        };

        private readonly AppDbContext _context;

        public UniversityAcademicCalendarService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<long> CreateAsync(AcademicCalendarRequestDto dto, LoggedInUserDto currentUser)
        {
            EnsureUniversityUser(currentUser);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                ValidateRequest(dto);
                EnsureUniversityAccess(currentUser, dto.UniversityId);
                await ValidateReuseReferenceAsync(dto, dto.UniversityId, null);
                await EnsureAcademicYearIsUniqueAsync(dto.UniversityId, dto.AcademicYearName.Trim(), null);

                var now = DateTime.UtcNow;
                var calendar = new KfUniversityAcademicCalendar
                {
                    UniversityId = dto.UniversityId,
                    AcademicYearName = dto.AcademicYearName.Trim(),
                    PeriodStartDate = dto.PeriodStartDate,
                    PeriodEndDate = dto.PeriodEndDate,
                    AcademicYearStartMonth = dto.AcademicYearStartMonth,
                    AcademicYearEndMonth = dto.AcademicYearEndMonth,
                    TermsPerYear = dto.TermsPerYear,
                    NamingStyle = NormalizeNamingStyle(dto.NamingStyle),
                    ReusePreviousCalendar = dto.ReusePreviousCalendar,
                    ReusedFromCalendarId = dto.ReusedFromCalendarId,
                    CreatedBy = currentUser.LoginId,
                    CreatedOn = now,
                    KfUniversityAcademicTerms = dto.Terms.Select(term => new KfUniversityAcademicTerm
                    {
                        TermSequence = term.TermSequence,
                        TermName = term.TermName.Trim(),
                        StartDate = term.StartDate,
                        EndDate = term.EndDate,
                        RegistrationStartDate = term.RegistrationStartDate,
                        RegistrationEndDate = term.RegistrationEndDate,
                        CreatedBy = currentUser.LoginId,
                        CreatedOn = now
                    }).ToList()
                };

                _context.KfUniversityAcademicCalendars.Add(calendar);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return calendar.AcademicCalendarId;
            }
            catch (DbUpdateException ex) when (IsAcademicYearUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                throw new CustomException("An academic calendar already exists for this university and academic year.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> UpdateAsync(long academicCalendarId, AcademicCalendarRequestDto dto, LoggedInUserDto currentUser)
        {
            EnsureUniversityUser(currentUser);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var calendar = await _context.KfUniversityAcademicCalendars
                    .Include(x => x.KfUniversityAcademicTerms)
                    .FirstOrDefaultAsync(x =>
                        x.AcademicCalendarId == academicCalendarId &&
                        currentUser.UniversityIds.Contains(x.UniversityId));

                if (calendar == null)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                EnsureUniversityAccess(currentUser, calendar.UniversityId);
                ValidateRequest(dto);
                if (dto.UniversityId != calendar.UniversityId)
                    throw new CustomException("An academic calendar cannot be moved to another university.");

                await ValidateReuseReferenceAsync(dto, calendar.UniversityId, calendar.AcademicCalendarId);
                await EnsureAcademicYearIsUniqueAsync(calendar.UniversityId, dto.AcademicYearName.Trim(), calendar.AcademicCalendarId);

                var now = DateTime.UtcNow;
                calendar.AcademicYearName = dto.AcademicYearName.Trim();
                calendar.PeriodStartDate = dto.PeriodStartDate;
                calendar.PeriodEndDate = dto.PeriodEndDate;
                calendar.AcademicYearStartMonth = dto.AcademicYearStartMonth;
                calendar.AcademicYearEndMonth = dto.AcademicYearEndMonth;
                calendar.TermsPerYear = dto.TermsPerYear;
                calendar.NamingStyle = NormalizeNamingStyle(dto.NamingStyle);
                calendar.ReusePreviousCalendar = dto.ReusePreviousCalendar;
                calendar.ReusedFromCalendarId = dto.ReusedFromCalendarId;
                calendar.UpdatedBy = currentUser.LoginId;
                calendar.UpdatedOn = now;

                var requestedSequences = dto.Terms.Select(x => x.TermSequence).ToHashSet();
                var existingBySequence = calendar.KfUniversityAcademicTerms.ToDictionary(x => x.TermSequence);

                foreach (var termDto in dto.Terms)
                {
                    if (existingBySequence.TryGetValue(termDto.TermSequence, out var term))
                    {
                        term.TermName = termDto.TermName.Trim();
                        term.StartDate = termDto.StartDate;
                        term.EndDate = termDto.EndDate;
                        term.RegistrationStartDate = termDto.RegistrationStartDate;
                        term.RegistrationEndDate = termDto.RegistrationEndDate;
                        term.UpdatedBy = currentUser.LoginId;
                        term.UpdatedOn = now;
                    }
                    else
                    {
                        calendar.KfUniversityAcademicTerms.Add(new KfUniversityAcademicTerm
                        {
                            TermSequence = termDto.TermSequence,
                            TermName = termDto.TermName.Trim(),
                            StartDate = termDto.StartDate,
                            EndDate = termDto.EndDate,
                            RegistrationStartDate = termDto.RegistrationStartDate,
                            RegistrationEndDate = termDto.RegistrationEndDate,
                            CreatedBy = currentUser.LoginId,
                            CreatedOn = now
                        });
                    }
                }

                var removedTerms = calendar.KfUniversityAcademicTerms
                    .Where(x => !requestedSequences.Contains(x.TermSequence))
                    .ToList();
                _context.KfUniversityAcademicTerms.RemoveRange(removedTerms);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return true;
            }
            catch (DbUpdateException ex) when (IsAcademicYearUniqueViolation(ex))
            {
                await transaction.RollbackAsync();
                throw new CustomException("An academic calendar already exists for this university and academic year.");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<AcademicCalendarResponseDto?> GetByIdAsync(long academicCalendarId, LoggedInUserDto currentUser)
        {
            EnsureUniversityUser(currentUser);

            var calendar = await _context.KfUniversityAcademicCalendars
                .AsNoTracking()
                .Include(x => x.KfUniversityAcademicTerms)
                .FirstOrDefaultAsync(x =>
                    x.AcademicCalendarId == academicCalendarId &&
                    currentUser.UniversityIds.Contains(x.UniversityId));

            return calendar == null ? null : ToResponse(calendar);
        }

        public async Task<List<AcademicCalendarResponseDto>> GetByUniversityAsync(long universityId, LoggedInUserDto currentUser)
        {
            EnsureUniversityUser(currentUser);
            EnsureUniversityAccess(currentUser, universityId);

            var calendars = await _context.KfUniversityAcademicCalendars
                .AsNoTracking()
                .Include(x => x.KfUniversityAcademicTerms)
                .Where(x => x.UniversityId == universityId)
                .OrderByDescending(x => x.PeriodStartDate)
                .ThenBy(x => x.AcademicYearName)
                .ToListAsync();

            return calendars.Select(ToResponse).ToList();
        }

        public async Task<AcademicCalendarResponseDto?> GetByAcademicYearAsync(long universityId, string academicYearName, LoggedInUserDto currentUser)
        {
            EnsureUniversityUser(currentUser);
            EnsureUniversityAccess(currentUser, universityId);

            if (string.IsNullOrWhiteSpace(academicYearName))
                throw new CustomException("Academic year name is required.");

            var normalizedName = academicYearName.Trim();
            var calendar = await _context.KfUniversityAcademicCalendars
                .AsNoTracking()
                .Include(x => x.KfUniversityAcademicTerms)
                .FirstOrDefaultAsync(x => x.UniversityId == universityId && x.AcademicYearName == normalizedName);

            return calendar == null ? null : ToResponse(calendar);
        }




        private static void EnsureUniversityUser(LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.University)
                throw new UnauthorizedAccessException();

            if (!currentUser.UniversityIds.Any())
                throw new UnauthorizedAccessException("User is not associated with a university.");
        }

        private static void EnsureUniversityAccess(LoggedInUserDto currentUser, long universityId)
        {
            if (universityId <= 0 || !currentUser.UniversityIds.Contains(universityId))
                throw new UnauthorizedAccessException();
        }

        private static void ValidateRequest(AcademicCalendarRequestDto dto)
        {
            if (dto == null)
                throw new CustomException("Academic calendar details are required.");
            if (dto.UniversityId <= 0)
                throw new CustomException("A valid university is required.");
            if (string.IsNullOrWhiteSpace(dto.AcademicYearName))
                throw new CustomException("Academic year name is required.");
            if (dto.AcademicYearName.Trim().Length > 40)
                throw new CustomException("Academic year name cannot exceed 40 characters.");
            if (dto.PeriodStartDate == DateOnly.MinValue)
                throw new CustomException("Academic period start date is required.");
            if (dto.PeriodEndDate == DateOnly.MinValue)
                throw new CustomException("Academic period end date is required.");
            if (dto.PeriodStartDate > dto.PeriodEndDate)
                throw new CustomException("Academic period start date must be on or before its end date.");
            if (dto.AcademicYearStartMonth is < 1 or > 12 || dto.AcademicYearEndMonth is < 1 or > 12)
                throw new CustomException("Academic year start and end months must be between 1 and 12.");
            if (dto.TermsPerYear is not (2 or 3 or 4))
                throw new CustomException("Terms per year must be 2, 3, or 4.");
            if (string.IsNullOrWhiteSpace(dto.NamingStyle) || !AllowedNamingStyles.Contains(dto.NamingStyle.Trim()))
                throw new CustomException("Naming style must be Term, Semester, Seasonal, or Custom.");
            if (dto.ReusedFromCalendarId.HasValue && !dto.ReusePreviousCalendar)
                throw new CustomException("A previous calendar can only be selected when calendar reuse is enabled.");
            if (dto.Terms == null || dto.Terms.Count != dto.TermsPerYear)
                throw new CustomException("The number of terms must match terms per year.");

            var sequences = new HashSet<byte>();
            foreach (var term in dto.Terms)
            {
                if (term == null)
                    throw new CustomException("Term details are required.");
                if (term.TermSequence < 1 || term.TermSequence > dto.TermsPerYear)
                    throw new CustomException("Each term sequence must be within the configured terms per year.");
                if (!sequences.Add(term.TermSequence))
                    throw new CustomException("Term sequences must be unique.");
                if (string.IsNullOrWhiteSpace(term.TermName))
                    throw new CustomException("Term name is required.");
                if (term.TermName.Trim().Length > 100)
                    throw new CustomException("Term name cannot exceed 100 characters.");
                if (term.StartDate == DateOnly.MinValue)
                    throw new CustomException("Term start date is required.");
                if (term.EndDate == DateOnly.MinValue)
                    throw new CustomException("Term end date is required.");
                if (term.RegistrationStartDate == DateOnly.MinValue)
                    throw new CustomException("Registration start date is required.");
                if (term.RegistrationEndDate == DateOnly.MinValue)
                    throw new CustomException("Registration end date is required.");
                if (term.StartDate > term.EndDate)
                    throw new CustomException("Term start date must be on or before its end date.");
                if (term.StartDate < dto.PeriodStartDate || term.EndDate > dto.PeriodEndDate)
                    throw new CustomException("Term dates must be within the academic calendar period.");
                if (term.RegistrationStartDate > term.RegistrationEndDate)
                    throw new CustomException("Registration start date must be on or before its end date.");
                if (term.RegistrationStartDate < dto.PeriodStartDate || term.RegistrationEndDate > dto.PeriodEndDate)
                    throw new CustomException("Registration dates must be within the academic calendar period.");
            }
        }

        private async Task ValidateReuseReferenceAsync(AcademicCalendarRequestDto dto, long universityId, long? currentCalendarId)
        {
            if (!dto.ReusedFromCalendarId.HasValue)
                return;

            if (dto.ReusedFromCalendarId == currentCalendarId)
                throw new CustomException("A calendar cannot reuse itself.");

            var sourceCalendarExists = await _context.KfUniversityAcademicCalendars
                .AnyAsync(x => x.AcademicCalendarId == dto.ReusedFromCalendarId.Value && x.UniversityId == universityId);

            if (!sourceCalendarExists)
                throw new CustomException("The selected previous calendar was not found for this university.");
        }

        private async Task EnsureAcademicYearIsUniqueAsync(long universityId, string academicYearName, long? excludedCalendarId)
        {
            var duplicateExists = await _context.KfUniversityAcademicCalendars.AnyAsync(x =>
                x.UniversityId == universityId &&
                x.AcademicYearName == academicYearName &&
                (!excludedCalendarId.HasValue || x.AcademicCalendarId != excludedCalendarId.Value));

            if (duplicateExists)
                throw new CustomException("An academic calendar already exists for this university and academic year.");
        }



        private static bool IsAcademicYearUniqueViolation(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException &&
                   (sqlException.Number == 2601 || sqlException.Number == 2627) &&
                   sqlException.Message.Contains("UQ_kf_acal_University_Year", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeNamingStyle(string namingStyle)
        {
            return namingStyle.Trim().ToLowerInvariant() switch
            {
                "term" => "Term",
                "semester" => "Semester",
                "seasonal" => "Seasonal",
                "custom" => "Custom",
                _ => throw new CustomException("Invalid naming style.")
            };
        }

        private static AcademicCalendarResponseDto ToResponse(KfUniversityAcademicCalendar calendar)
        {
            return new AcademicCalendarResponseDto
            {
                AcademicCalendarId = calendar.AcademicCalendarId,
                UniversityId = calendar.UniversityId,
                AcademicYearName = calendar.AcademicYearName,
                PeriodStartDate = calendar.PeriodStartDate,
                PeriodEndDate = calendar.PeriodEndDate,
                AcademicYearStartMonth = calendar.AcademicYearStartMonth,
                AcademicYearEndMonth = calendar.AcademicYearEndMonth,
                TermsPerYear = calendar.TermsPerYear,
                NamingStyle = calendar.NamingStyle,
                ReusePreviousCalendar = calendar.ReusePreviousCalendar,
                ReusedFromCalendarId = calendar.ReusedFromCalendarId,
                CreatedOn = calendar.CreatedOn,
                UpdatedOn = calendar.UpdatedOn,
                Terms = calendar.KfUniversityAcademicTerms
                    .OrderBy(x => x.TermSequence)
                    .Select(term => new AcademicTermResponseDto
                    {
                        AcademicTermId = term.AcademicTermId,
                        TermSequence = term.TermSequence,
                        TermName = term.TermName,
                        StartDate = term.StartDate,
                        EndDate = term.EndDate,
                        RegistrationStartDate = term.RegistrationStartDate,
                        RegistrationEndDate = term.RegistrationEndDate,
                        CreatedOn = term.CreatedOn,
                        UpdatedOn = term.UpdatedOn
                    }).ToList()
            };
        }
    }
}
