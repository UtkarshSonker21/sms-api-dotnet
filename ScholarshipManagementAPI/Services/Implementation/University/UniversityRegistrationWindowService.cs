using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.University.RegistrationWindows;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.University;

namespace ScholarshipManagementAPI.Services.Implementation.University
{
    public class UniversityRegistrationWindowService : IUniversityRegistrationWindowService
    {
        private readonly AppDbContext _context;

        public UniversityRegistrationWindowService(AppDbContext context) => _context = context;



        public async Task<UniversityRegistrationWindowsResponseDto> GetForUserAsync(LoggedInUserDto currentUser)
        {
            var universityIds = GetUniversityIds(currentUser);
            var universities = await _context.KfUniversities.AsNoTracking()
                .Where(x => universityIds.Contains(x.UniversityId))
                .OrderBy(x => x.UniversityName)
                .Select(x => new UniversityOptionDto { UniversityId = x.UniversityId, UniversityName = x.UniversityName })
                .ToListAsync();

            var windows = await _context.KfUniversityRegistrationWindows.AsNoTracking()
                .Where(x => universityIds.Contains(x.UniversityId))
                .OrderByDescending(x => x.AcademicYearStart)
                .ThenBy(x => x.PeriodType)
                .Select(x => new
                {
                    x.UniversityRegistrationWindowId,
                    x.UniversityId,
                    x.PeriodType,
                    x.AcademicYearStart,
                    x.RegistrationFrom,
                    x.RegistrationTo,
                    x.IsOpen,
                    x.Notes,
                    x.RowVersion
                })
                .ToListAsync();

            var names = universities.ToDictionary(x => x.UniversityId, x => x.UniversityName);
            var now = DateTime.UtcNow;
            return new UniversityRegistrationWindowsResponseDto
            {
                Universities = universities,
                Windows = windows.Select(x => new UniversityRegistrationWindowResponseDto
                {
                    UniversityRegistrationWindowId = x.UniversityRegistrationWindowId,
                    UniversityId = x.UniversityId,
                    UniversityName = names.GetValueOrDefault((long)x.UniversityId, string.Empty),
                    PeriodType = x.PeriodType,
                    AcademicYearStart = x.AcademicYearStart,
                    PeriodLabel = FormatPeriodLabel((AcademicPeriodType)x.PeriodType, x.AcademicYearStart),
                    RegistrationFrom = x.RegistrationFrom,
                    RegistrationTo = x.RegistrationTo,
                    IsOpen = x.IsOpen,
                    IsCurrentlyOpen = x.IsOpen && x.RegistrationFrom.HasValue && x.RegistrationFrom.Value <= now && x.RegistrationTo >= now,
                    Notes = x.Notes,
                    RowVersion = Convert.ToBase64String(x.RowVersion)
                }).ToList()
            };
        }

        public async Task<long> OpenOrUpdateAsync(UniversityRegistrationWindowRequestDto request, LoggedInUserDto currentUser)
        {
            var universityIds = GetUniversityIds(currentUser);
            if (!universityIds.Contains(request.UniversityId))
                throw new UnauthorizedAccessException("You do not have access to this university.");
            if (!Enum.IsDefined(typeof(AcademicPeriodType), request.PeriodType))
                throw new CustomException("Select a valid academic period.");
            if (string.IsNullOrWhiteSpace(request.Notes) == false && request.Notes.Length > 500)
                throw new CustomException("Notes cannot exceed 500 characters.");

            if (request.RegistrationFrom == default)
                throw new CustomException("Registration opening date is required.");
            if (request.RegistrationTo == default)
                throw new CustomException("Registration closing date is required.");

            var registrationFrom = request.RegistrationFrom.Kind == DateTimeKind.Utc
                ? request.RegistrationFrom
                : request.RegistrationFrom.ToUniversalTime();
            var registrationTo = request.RegistrationTo.Kind == DateTimeKind.Utc
                ? request.RegistrationTo
                : request.RegistrationTo.ToUniversalTime();
            if (registrationFrom >= registrationTo)
                throw new CustomException("Registration opening date must be earlier than the closing date.");
            if (registrationTo <= DateTime.UtcNow)
                throw new CustomException("Registration deadline must be in the future.");

            int universityId;
            int loginId;
            try
            {
                universityId = checked((int)request.UniversityId);
                loginId = checked((int)currentUser.LoginId);
            }
            catch (OverflowException)
            {
                throw new CustomException("The university or user identifier is outside the supported database range.");
            }

            var entity = await _context.KfUniversityRegistrationWindows.FirstOrDefaultAsync(x =>
                x.UniversityId == universityId &&
                x.PeriodType == request.PeriodType &&
                x.AcademicYearStart == request.AcademicYearStart);

            if (entity == null)
            {
                entity = new KfUniversityRegistrationWindow
                {
                    UniversityId = universityId,
                    PeriodType = request.PeriodType,
                    AcademicYearStart = request.AcademicYearStart,
                    RegistrationFrom = registrationFrom,
                    RegistrationTo = registrationTo,
                    IsOpen = true,
                    Notes = request.Notes?.Trim(),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = loginId
                };
                _context.KfUniversityRegistrationWindows.Add(entity);
            }
            else
            {
                ApplyConcurrencyToken(entity, request.RowVersion);
                entity.RegistrationFrom = registrationFrom;
                entity.RegistrationTo = registrationTo;
                entity.IsOpen = true;
                entity.Notes = request.Notes?.Trim();
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = loginId;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new CustomException("This registration window was changed by another user. Reload it and try again.");
            }
            catch (DbUpdateException)
            {
                throw new CustomException("The registration window could not be saved. Reload the list and try again.");
            }

            return entity.UniversityRegistrationWindowId;
        }

        public async Task CloseAsync(long id, CloseUniversityRegistrationWindowRequestDto request, LoggedInUserDto currentUser)
        {
            var universityIds = GetUniversityIds(currentUser);
            var entity = await _context.KfUniversityRegistrationWindows.FirstOrDefaultAsync(x =>
                x.UniversityRegistrationWindowId == id && universityIds.Contains(x.UniversityId));
            if (entity == null)
                throw new CustomException("Registration window was not found.");

            ApplyConcurrencyToken(entity, request.RowVersion);
            entity.IsOpen = false;
            entity.UpdatedAt = DateTime.UtcNow;
            try
            {
                entity.UpdatedBy = checked((int)currentUser.LoginId);
                await _context.SaveChangesAsync();
            }
            catch (OverflowException)
            {
                throw new CustomException("The user identifier is outside the supported database range.");
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new CustomException("This registration window was changed by another user. Reload it and try again.");
            }
            catch (DbUpdateException)
            {
                throw new CustomException("The registration window could not be closed. Reload the list and try again.");
            }
        }



        private static long[] GetUniversityIds(LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.University)
                throw new UnauthorizedAccessException();
            if (currentUser.UniversityIds.Count == 0)
                throw new UnauthorizedAccessException("User is not associated with a university.");
            return currentUser.UniversityIds.Distinct().ToArray();
        }

        private void ApplyConcurrencyToken(KfUniversityRegistrationWindow entity, string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                throw new CustomException("Reload the registration window before updating it.");
            byte[] rowVersion;
            try { rowVersion = Convert.FromBase64String(token); }
            catch (FormatException) { throw new CustomException("The registration window version is invalid. Reload and try again."); }
            if (rowVersion.Length != 8)
                throw new CustomException("The registration window version is invalid. Reload and try again.");
            _context.Entry(entity).Property(x => x.RowVersion).OriginalValue = rowVersion;
        }

        private static string FormatPeriodLabel(AcademicPeriodType periodType, int year) => periodType switch
        {
            AcademicPeriodType.Summer => $"Summer {year}",
            AcademicPeriodType.Fall => $"Fall {year}/{year + 1}",
            AcademicPeriodType.Spring => $"Spring {year}/{year + 1}",
            AcademicPeriodType.Winter => $"Winter {year}/{year + 1}",
            AcademicPeriodType.SpringSummer => $"Spring–Summer {year}/{year + 1}",
            AcademicPeriodType.FallWinter => $"Fall–Winter {year}/{year + 1}",
            _ => string.Empty
        };
    }
}
