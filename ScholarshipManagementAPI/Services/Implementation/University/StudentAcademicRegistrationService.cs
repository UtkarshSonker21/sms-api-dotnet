using Microsoft.EntityFrameworkCore;
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
        private readonly AppDbContext _context;

        public StudentAcademicRegistrationService(AppDbContext context)
        {
            _context = context;
        }


        public async Task<bool> RegisterStudentAsync(RegisterStudentRequestDto dto, LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.University)
                throw new UnauthorizedAccessException();

            if (!currentUser.UniversityIds.Any())
                throw new UnauthorizedAccessException("User is not associated with a university.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

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

                var now = DateTime.UtcNow;
                var centralRegistrationOpen = await _context.KfUniversityRegistrationWindows
                    .AsNoTracking()
                    .AnyAsync(x => x.UniversityId == application.Program.UniversityId &&
                                   x.IsOpen &&
                                   x.RegistrationFrom.HasValue &&
                                   x.RegistrationFrom.Value <= now &&
                                   x.RegistrationTo >= now);
                if (!centralRegistrationOpen)
                    throw new CustomException("University registration is not currently open.");

                var alreadyRegistered = await _context.KfStudentAcademicRegistrations
                    .AnyAsync(x => x.ApplicationId == dto.ApplicationId);

                if (alreadyRegistered)
                    throw new CustomException("Student has already been registered.");

                _context.KfStudentAcademicRegistrations.Add(new KfStudentAcademicRegistration
                {
                    StudentId = application.StudentId,
                    ApplicationId = application.ApplicationId,
                    ProgramId = application.ProgramId,
                    SemesterNo = dto.SemesterNo,
                    RegistrationDate = dto.RegistrationDate,
                    Remarks = dto.Remarks,

                    CreatedBy = currentUser.LoginId,
                    CreatedOn = DateTime.UtcNow
                });

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

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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
