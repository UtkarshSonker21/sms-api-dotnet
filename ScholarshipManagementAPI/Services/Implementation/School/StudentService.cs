using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.School.Students;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.School;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Amazon.S3.Util.S3EventNotification;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace ScholarshipManagementAPI.Services.Implementation.School
{
    public class StudentService : IStudentService
    {
        private readonly AppDbContext _context;

        public StudentService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<long> CreateAsync(StudentRequestDto dto, LoggedInUserDto currentUser)
        {
            if (await _context.KfStudentRegistrations.AnyAsync(x => x.Phone == dto.Phone))
            {
                throw new CustomException("Student with same mobile number already exists");
            }

            if (await _context.KfStudentRegistrations.AnyAsync(x => x.Email == dto.Email))
            {
                throw new CustomException("Student with same email already exists");
            }

            // Find school name from school id if possible
            string? studentCode = null;
            if (dto.SchoolId <= 0)
            {
                throw new CustomException("Please select a valid school.");
            }

            if (currentUser.StaffType == StaffType.School &&
                !currentUser.SchoolIds.Contains(dto.SchoolId))
            {
                throw new UnauthorizedAccessException();
            }

            var school = await _context.KfSchools
                .Where(x => x.SchoolId == dto.SchoolId)
                .Select(x => new
                {
                    x.StudentCodeFormatPrefix,
                    x.StudentCodeFormatSuffix,
                    x.StudentSequenceNumber,
                })
                .FirstOrDefaultAsync();

            if (school == null)
            {
                throw new CustomException("Selected school does not exist.");
            }

            var prefix = string.IsNullOrWhiteSpace(school.StudentCodeFormatPrefix)
                ? "SCH"
                : school.StudentCodeFormatPrefix;

            var suffix = string.IsNullOrWhiteSpace(school.StudentCodeFormatSuffix)
                ? "#"
                : school.StudentCodeFormatSuffix;

            var studentCount = await _context.KfStudentRegistrations
                .CountAsync(x => x.SchoolId == dto.SchoolId);

            var nextSequence = school.StudentSequenceNumber + studentCount;

            studentCode = $"{prefix}-{nextSequence:D4}-{suffix}";

            var entity = new KfStudentRegistration
            {
                StudentCode = studentCode,
                PhotoPath = dto.PhotoPath,

                FirstName = dto.FirstName,
                SecondName = dto.SecondName,
                ThirdName = dto.ThirdName,
                LastName = dto.LastName,

                MotherName = dto.MotherName,

                Dob = dto.Dob.HasValue
                ? DateOnly.FromDateTime(dto.Dob.Value)
                : null,

                NationalityId = dto.NationalityId,
                ResidenceCountryId = dto.ResidenceCountryId,
                ReligionId = dto.ReligionId,
                GenderId = dto.GenderId,

                Tribe = dto.Tribe,

                IsOrphan = dto.IsOrphan,
                OrphanNumber = dto.OrphanNumber,

                City = dto.City,
                Village = dto.Village,
                Block = dto.Block,
                Street = dto.Street,
                House = dto.House,

                Phone = dto.Phone,
                Email = dto.Email,

                FromDaSchool = dto.FromDaSchool,
                DaStudentCode = dto.DaStudentCode,
                SchoolId = dto.SchoolId,

                HsSpecialization = dto.HsSpecialization,
                TanzanianStudentCombination = dto.TanzanianStudentCombination,

                TotalScore = dto.TotalScore,
                MaxScore = dto.MaxScore,
                RelativeGrade = dto.RelativeGrade,
                EnglishScore = dto.EnglishScore,

                TransferInstitution = dto.TransferInstitution,
                TransferProgram = dto.TransferProgram,
                TransferInstitutionType = dto.TransferInstitutionType,
                TransferCredits = dto.TransferCredits,
                TransferLastSemEnd = dto.TransferLastSemEnd.HasValue
                ? DateOnly.FromDateTime(dto.TransferLastSemEnd.Value)
                : null,
                TransferGpa = dto.TransferGpa,

                FinancialNeedStatusId = dto.FinancialNeedStatusId,
                SelfRelianceLevelId = dto.SelfRelianceLevelId,
                MotivationLevelId = dto.MotivationLevelId,
                FutureGoalsLevelId = dto.FutureGoalsLevelId,

                RecommendationLetterPath = dto.RecommendationLetterPath,
                RecommendationLetterNotes = dto.RecommendationLetterNotes,

                IsDraft = dto.IsDraft,
                IsActive = true,

                CreatedBy = currentUser.LoginId,
                CreatedDate = DateTime.UtcNow
            };

            _context.KfStudentRegistrations.Add(entity);
            await _context.SaveChangesAsync();

            return entity.StudentId;
        }

        public async Task<bool> UpdateAsync(StudentRequestDto dto, LoggedInUserDto currentUser)
        {
            if (dto.StudentId == null || dto.StudentId == 0)
                return false;

            var entity = await _context.KfStudentRegistrations
                .FirstOrDefaultAsync(x => x.StudentId == dto.StudentId);

            if (entity == null)
                return false;

            if (currentUser.StaffType == StaffType.School &&
                !currentUser.SchoolIds.Contains(entity.SchoolId))
            {
                throw new UnauthorizedAccessException();
            }


            if (await _context.KfStudentRegistrations.AnyAsync(x => x.Phone == dto.Phone && x.StudentId != dto.StudentId))
            {
                throw new CustomException("Student with same mobile number already exists");
            }

            if (await _context.KfStudentRegistrations.AnyAsync(x => x.Email == dto.Email && x.StudentId != dto.StudentId))
            {
                throw new CustomException("Student with same email already exists");
            }

            if (currentUser.StaffType == StaffType.School &&
                !currentUser.SchoolIds.Contains(dto.SchoolId))
            {
                throw new UnauthorizedAccessException();
            }

            // ---------------------------------------------------------
            // Check whether student already has an active application
            // ---------------------------------------------------------

            var activeStatuses = new[]
            {
        (int)StudentApplicationStatus.Draft,
        (int)StudentApplicationStatus.AcceptanceInProcess,
        (int)StudentApplicationStatus.Sponsored,
        (int)StudentApplicationStatus.Awarded,
        (int)StudentApplicationStatus.Registered
    };

            bool hasActiveApplication = await _context.KfStudentProgramApplications
                .AnyAsync(x =>
                    x.StudentId == entity.StudentId &&
                    activeStatuses.Contains(x.ApplicationStatus));

            if (hasActiveApplication)
            {
                // Academic Information
                bool academicInformationChanged =
                    entity.HsSpecialization != dto.HsSpecialization ||
                    entity.TanzanianStudentCombination != dto.TanzanianStudentCombination ||
                    entity.TotalScore != dto.TotalScore ||
                    entity.MaxScore != dto.MaxScore ||
                    entity.RelativeGrade != dto.RelativeGrade ||
                    entity.EnglishScore != dto.EnglishScore;

                // Transfer Information
                var dtoTransferLastSemEnd = dto.TransferLastSemEnd.HasValue
                    ? DateOnly.FromDateTime(dto.TransferLastSemEnd.Value)
                    : (DateOnly?)null;

                bool transferInformationChanged =
                    entity.TransferInstitution != dto.TransferInstitution ||
                    entity.TransferProgram != dto.TransferProgram ||
                    entity.TransferInstitutionType != dto.TransferInstitutionType ||
                    entity.TransferCredits != dto.TransferCredits ||
                    entity.TransferLastSemEnd != dtoTransferLastSemEnd ||
                    entity.TransferGpa != dto.TransferGpa;

                // Behavioral & Social Evaluation
                bool evaluationInformationChanged =
                    entity.FinancialNeedStatusId != dto.FinancialNeedStatusId ||
                    entity.SelfRelianceLevelId != dto.SelfRelianceLevelId ||
                    entity.MotivationLevelId != dto.MotivationLevelId ||
                    entity.FutureGoalsLevelId != dto.FutureGoalsLevelId;

                // Recommendation
                bool recommendationInformationChanged =
                    entity.RecommendationLetterPath != dto.RecommendationLetterPath ||
                    entity.RecommendationLetterNotes != dto.RecommendationLetterNotes;

                if (academicInformationChanged)
                {
                    throw new CustomException(
                        "Student has an active program application. Academic information cannot be changed."
                    );
                }

                if (transferInformationChanged)
                {
                    throw new CustomException(
                        "Student has an active program application. Transfer information cannot be changed."
                    );
                }

                if (evaluationInformationChanged)
                {
                    throw new CustomException(
                        "Student has an active program application. Behavioral and social evaluation information cannot be changed."
                    );
                }

                if (recommendationInformationChanged)
                {
                    throw new CustomException(
                        "Student has an active program application. Recommendation information cannot be changed."
                    );
                }
            }

            // ---------------------------------------------------------
            // Update Student Information
            // ---------------------------------------------------------

            entity.PhotoPath = dto.PhotoPath;

            //entity.StudentCode = dto.StudentCode;

            entity.FirstName = dto.FirstName;
            entity.SecondName = dto.SecondName;
            entity.ThirdName = dto.ThirdName;
            entity.LastName = dto.LastName;

            entity.MotherName = dto.MotherName;

            entity.Dob = dto.Dob.HasValue
                ? DateOnly.FromDateTime(dto.Dob.Value)
                : null;

            entity.NationalityId = dto.NationalityId;
            entity.ResidenceCountryId = dto.ResidenceCountryId;
            entity.ReligionId = dto.ReligionId;
            entity.GenderId = dto.GenderId;

            entity.Tribe = dto.Tribe;

            entity.IsOrphan = dto.IsOrphan;
            entity.OrphanNumber = dto.OrphanNumber;

            entity.City = dto.City;
            entity.Village = dto.Village;
            entity.Block = dto.Block;
            entity.Street = dto.Street;
            entity.House = dto.House;

            entity.Phone = dto.Phone;
            entity.Email = dto.Email;

            entity.FromDaSchool = dto.FromDaSchool;
            entity.DaStudentCode = dto.DaStudentCode;
            entity.SchoolId = dto.SchoolId;

            entity.HsSpecialization = dto.HsSpecialization;
            entity.TanzanianStudentCombination = dto.TanzanianStudentCombination;

            entity.TotalScore = dto.TotalScore;
            entity.MaxScore = dto.MaxScore;
            entity.RelativeGrade = dto.RelativeGrade;
            entity.EnglishScore = dto.EnglishScore;

            entity.TransferInstitution = dto.TransferInstitution;
            entity.TransferProgram = dto.TransferProgram;
            entity.TransferInstitutionType = dto.TransferInstitutionType;
            entity.TransferCredits = dto.TransferCredits;
            entity.TransferLastSemEnd = dto.TransferLastSemEnd.HasValue
                ? DateOnly.FromDateTime(dto.TransferLastSemEnd.Value)
                : null;
            entity.TransferGpa = dto.TransferGpa;

            entity.FinancialNeedStatusId = dto.FinancialNeedStatusId;
            entity.SelfRelianceLevelId = dto.SelfRelianceLevelId;
            entity.MotivationLevelId = dto.MotivationLevelId;
            entity.FutureGoalsLevelId = dto.FutureGoalsLevelId;

            entity.RecommendationLetterPath = dto.RecommendationLetterPath;
            entity.RecommendationLetterNotes = dto.RecommendationLetterNotes;

            entity.IsDraft = dto.IsDraft;

            entity.UpdatedBy = currentUser.LoginId;
            entity.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(long id, LoggedInUserDto currentUser)
        {
            var entity = await _context.KfStudentRegistrations
                .FirstOrDefaultAsync(x => x.StudentId == id);

            if (entity == null)
                return false;

            if (currentUser.StaffType == StaffType.School &&
                !currentUser.SchoolIds.Contains(entity.SchoolId))
            {
                throw new UnauthorizedAccessException();
            }

            entity.IsActive = false;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<StudentRequestDto?> GetByIdAsync(long id, LoggedInUserDto currentUser)
        {
            var x = await _context.KfStudentRegistrations
                .AsNoTracking()
                .Include(s => s.School)
                .Include(s => s.Nationality)
                .Include(s => s.ResidenceCountry)
                .Include(s => s.Gender)
                .Include(s => s.Religion)
                .Include(s => s.FinancialNeedStatus)
                .Include(s => s.SelfRelianceLevel)
                .Include(s => s.MotivationLevel)
                .Include(s => s.FutureGoalsLevel)
                .Include(s => s.CreatedByNavigation)
                .Include(s => s.UpdatedByNavigation)
                .FirstOrDefaultAsync(s => s.StudentId == id && s.IsActive);

            if (x == null)
                return null;

            if (currentUser.StaffType == StaffType.School &&
                !currentUser.SchoolIds.Contains(x.SchoolId))
            {
                throw new UnauthorizedAccessException();
            }

            return new StudentRequestDto
            {
                StudentId = x.StudentId,
                StudentCode = x.StudentCode,

                PhotoPath = x.PhotoPath,

                FirstName = x.FirstName,
                SecondName = x.SecondName,
                ThirdName = x.ThirdName,
                LastName = x.LastName,
                MotherName = x.MotherName,

                FullName = string.Join(" ",
                    new[]
                    {
                x.FirstName,
                x.SecondName,
                x.ThirdName,
                x.LastName
                    }.Where(s => !string.IsNullOrWhiteSpace(s))),

                Dob = x.Dob?.ToDateTime(TimeOnly.MinValue),

                NationalityId = x.NationalityId,
                ResidenceCountryId = x.ResidenceCountryId,
                ReligionId = x.ReligionId,
                GenderId = x.GenderId,

                Tribe = x.Tribe,

                IsOrphan = x.IsOrphan,
                OrphanNumber = x.OrphanNumber,

                City = x.City,
                Village = x.Village,
                Block = x.Block,
                Street = x.Street,
                House = x.House,

                Phone = x.Phone,
                Email = x.Email,

                FromDaSchool = x.FromDaSchool,
                DaStudentCode = x.DaStudentCode,
                SchoolId = x.SchoolId,

                HsSpecialization = x.HsSpecialization,
                TanzanianStudentCombination = x.TanzanianStudentCombination,

                TotalScore = x.TotalScore,
                MaxScore = x.MaxScore,
                RelativeGrade = x.RelativeGrade,
                EnglishScore = x.EnglishScore,

                TransferInstitution = x.TransferInstitution,
                TransferProgram = x.TransferProgram,
                TransferInstitutionType = x.TransferInstitutionType,
                TransferCredits = x.TransferCredits,
                TransferLastSemEnd = x.TransferLastSemEnd?.ToDateTime(TimeOnly.MinValue),
                TransferGpa = x.TransferGpa,

                FinancialNeedStatusId = x.FinancialNeedStatusId,
                SelfRelianceLevelId = x.SelfRelianceLevelId,
                MotivationLevelId = x.MotivationLevelId,
                FutureGoalsLevelId = x.FutureGoalsLevelId,

                RecommendationLetterPath = x.RecommendationLetterPath,
                RecommendationLetterNotes = x.RecommendationLetterNotes,

                IsDraft = x.IsDraft,
                IsActive = x.IsActive,

                CreatedBy = x.CreatedBy,
                CreatedDate = x.CreatedDate,
                UpdatedBy = x.UpdatedBy,
                UpdatedDate = x.UpdatedDate,

                // ===========================
                // Response Only
                // ===========================

                SchoolName = x.School?.SchoolName,

                NationalityName = x.Nationality?.CountryName,
                ResidenceCountryName = x.ResidenceCountry?.CountryName,

                GenderName = x.Gender?.DisplayText,
                ReligionName = x.Religion?.DisplayText,

                FinancialNeedStatusName = x.FinancialNeedStatus?.DisplayText,
                SelfRelianceLevelName = x.SelfRelianceLevel?.DisplayText,
                MotivationLevelName = x.MotivationLevel?.DisplayText,
                FutureGoalsLevelName = x.FutureGoalsLevel?.DisplayText,

            };
        }
        
        
        public async Task<PagedResultDto<StudentRequestDto>> GetByFilterAsync(StudentFilterDto filter, LoggedInUserDto currentUser)
        {
            var query = _context.KfStudentRegistrations
                .AsNoTracking()
                .Where(x => x.IsActive)
                .AsQueryable();

            if (currentUser.StaffType == StaffType.School)
            {
                query = query.Where(x =>
                    currentUser.SchoolIds.Contains(x.SchoolId));
            }

            // for coordinator nominations
            if (filter.MyNominations.HasValue)
            {
                query = query.Where(x => x.KfStudentProgramApplications.Any(a =>
                      a.CreatedBy == filter.MyNominations.Value));
            }

            if (filter.UniversityId.HasValue)
            {
                query = query.Where(x =>
                    x.KfStudentProgramApplications.Any(a =>
                        a.Program.UniversityId == filter.UniversityId.Value));
            }

            if (filter.FacultyId.HasValue)
            {
                query = query.Where(x =>
                    x.KfStudentProgramApplications.Any(a =>
                        a.Program.FacultyId == filter.FacultyId.Value));
            }


            if (filter.SchoolId.HasValue)
            {
                if (currentUser.StaffType == StaffType.School &&
                    !currentUser.SchoolIds.Contains(filter.SchoolId.Value))
                {
                    throw new UnauthorizedAccessException();
                }

                query = query.Where(x => x.SchoolId == filter.SchoolId);
            }

            if (filter.GenderId.HasValue)
                query = query.Where(x => x.GenderId == filter.GenderId);

            if (filter.ReligionId.HasValue)
                query = query.Where(x => x.ReligionId == filter.ReligionId);

            if (filter.FromDaSchool.HasValue)
                query = query.Where(x => x.FromDaSchool == filter.FromDaSchool);

            if (filter.IsOrphan.HasValue)
                query = query.Where(x => x.IsOrphan == filter.IsOrphan);

            if (filter.IsDraft.HasValue)
                query = query.Where(x => x.IsDraft == filter.IsDraft);

            if (filter.IsActive.HasValue)
                query = query.Where(x => x.IsActive == filter.IsActive);

            if (filter.StudentStatusId.HasValue)
            {
                query = query.Where(x =>
                    x.KfStudentProgramApplications.Any(a => a.ApplicationStatus == filter.StudentStatusId.Value));
            }

            if (!string.IsNullOrWhiteSpace(filter.HsSpecialization))
            {
                query = query.Where(x =>
                    x.HsSpecialization != null &&
                    x.HsSpecialization.Contains(filter.HsSpecialization));
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim();

                query = query.Where(x =>
                    x.FirstName.Contains(search) ||
                    (x.SecondName != null && x.SecondName.Contains(search)) ||
                    (x.ThirdName != null && x.ThirdName.Contains(search)) ||
                    x.LastName.Contains(search) ||
                    (x.Email != null && x.Email.Contains(search)) ||
                    (x.Phone != null && x.Phone.Contains(search)) ||
                    (x.DaStudentCode != null && x.DaStudentCode.Contains(search)) ||

                    // for coordinator nominations
                    x.KfStudentProgramApplications.Any(a =>
                         a.Program.ProgramName.Contains(search) ||
                         a.Program.ProgramCode.Contains(search) ||
                         a.Program.Faculty.FacultyName.Contains(search) ||
                         a.Program.University.UniversityName.Contains(search))

                );
            }

            var totalCount = await query.CountAsync();

            query = query.OrderByDescending(x => x.StudentId);

            if (filter.PageSize > 0)
            {
                query = query
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize);
            }

            var items = await query
                .Select(x => new StudentRequestDto
                {
                    StudentId = x.StudentId,

                    PhotoPath = x.PhotoPath,

                    FirstName = x.FirstName,
                    SecondName = x.SecondName,
                    ThirdName = x.ThirdName,
                    LastName = x.LastName,

                    Dob = x.Dob.HasValue
                        ? x.Dob.Value.ToDateTime(TimeOnly.MinValue)
                        : null,

                    StudentCode = x.StudentCode,

                    SchoolId = x.SchoolId,
                    SchoolName = x.School != null ? x.School.SchoolName : null,

                    NationalityId = x.NationalityId,
                    NationalityName = x.Nationality != null ? x.Nationality.CountryName : null,

                    ResidenceCountryId = x.ResidenceCountryId,
                    ResidenceCountryName = x.ResidenceCountry != null ? x.ResidenceCountry.CountryName : null,

                    GenderId = x.GenderId,
                    GenderName = x.Gender != null ? x.Gender.DisplayText : null,

                    ReligionId = x.ReligionId,
                    ReligionName = x.Religion != null ? x.Religion.DisplayText : null,

                    FinancialNeedStatusId = x.FinancialNeedStatusId,
                    FinancialNeedStatusName = x.FinancialNeedStatus != null ? x.FinancialNeedStatus.DisplayText : null,

                    SelfRelianceLevelId = x.SelfRelianceLevelId,
                    SelfRelianceLevelName = x.SelfRelianceLevel != null ? x.SelfRelianceLevel.DisplayText : null,

                    MotivationLevelId = x.MotivationLevelId,
                    MotivationLevelName = x.MotivationLevel != null ? x.MotivationLevel.DisplayText : null,

                    FutureGoalsLevelId = x.FutureGoalsLevelId,
                    FutureGoalsLevelName = x.FutureGoalsLevel != null ? x.FutureGoalsLevel.DisplayText : null,

                    Phone = x.Phone,
                    Email = x.Email,

                    IsOrphan = x.IsOrphan,
                    FromDaSchool = x.FromDaSchool,
                    DaStudentCode = x.DaStudentCode,

                    IsDraft = x.IsDraft,
                    IsActive = x.IsActive,

                    CreatedBy = x.CreatedBy,
                    CreatedDate = x.CreatedDate,
                    UpdatedBy = x.UpdatedBy,
                    UpdatedDate = x.UpdatedDate,

                    StudentApplicationStatusId = x.KfStudentProgramApplications
                        .OrderByDescending(a => a.ApplicationId)
                        .Select(a => (long?)a.ApplicationStatus)
                        .FirstOrDefault(),

                    StudentAssignedProgramName = x.KfStudentProgramApplications
                        .OrderByDescending(a => a.ApplicationId)
                        .Select(a => a.Program.ProgramName + " (" + a.Program.ProgramCode + ")")
                        .FirstOrDefault(), 

                    StudentAssignedUniversityName = x.KfStudentProgramApplications
                        .OrderByDescending(a => a.ApplicationId)
                        .Select(a => a.Program.University.UniversityName)
                        .FirstOrDefault(),

                    StudentAssignedUniversityId = x.KfStudentProgramApplications
                        .OrderByDescending(a => a.ApplicationId)
                        .Select(a => (long?)a.Program.UniversityId)
                        .FirstOrDefault(),

                    TotalScore = x.TotalScore,
                    MaxScore = x.MaxScore,
                    RelativeGrade = x.RelativeGrade,
                    EnglishScore = x.EnglishScore,
                    HsSpecialization = x.HsSpecialization,
                    TanzanianStudentCombination = x.TanzanianStudentCombination

                })
                .ToListAsync();

            foreach (var item in items)
            {
                item.FullName = string.Join(" ",
                    new[]
                    {
                item.FirstName,
                item.SecondName,
                item.ThirdName,
                item.LastName
                    }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            return new PagedResultDto<StudentRequestDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }



        public async Task<string> UploadProfilePhotoAsync(long studentId, IFormFile file, LoggedInUserDto currentUser)
        {
            var student = await _context.KfStudentRegistrations
                .FirstOrDefaultAsync(x => x.StudentId == studentId && x.IsActive);

            if (student == null)
                throw new CustomException("Student not found.");

            if (currentUser.StaffType == StaffType.School &&
                !currentUser.SchoolIds.Contains(student.SchoolId))
            {
                throw new UnauthorizedAccessException();
            }

            if (file == null || file.Length == 0)
                throw new CustomException("Please select a valid profile photo.");

            var folder = Path.Combine(
                "wwwroot",
                "uploads",
                "students",
                student.StudentCode,
                "profile-photo");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // Delete old photo
            if (!string.IsNullOrWhiteSpace(student.PhotoPath))
            {
                var oldFile = Path.Combine(Directory.GetCurrentDirectory(),
                    student.PhotoPath.TrimStart('/', '\\')
                        .Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(oldFile))
                    File.Delete(oldFile);
            }

            var extension = Path.GetExtension(file.FileName);

            var storedFileName = $"profile_{Guid.NewGuid():N}{extension}";

            var physicalPath = Path.Combine(folder, storedFileName);

            await using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            student.PhotoPath =
                $"/uploads/students/{student.StudentCode}/profile-photo/{storedFileName}";

            student.UpdatedBy = currentUser.LoginId;
            student.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return student.PhotoPath;
        }

        public async Task<bool> DeleteProfilePhotoAsync(long studentId, LoggedInUserDto currentUser)
        {
            var student = await _context.KfStudentRegistrations
                .FirstOrDefaultAsync(x => x.StudentId == studentId && x.IsActive);

            if (student == null)
                throw new CustomException("Student not found.");

            if (currentUser.StaffType == StaffType.School &&
                !currentUser.SchoolIds.Contains(student.SchoolId))
            {
                throw new UnauthorizedAccessException();
            }

            if (string.IsNullOrWhiteSpace(student.PhotoPath))
                return true;

            var physicalPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                student.PhotoPath.TrimStart('/', '\\')
                    .Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(physicalPath))
                File.Delete(physicalPath);

            student.PhotoPath = null;
            student.UpdatedBy = currentUser.LoginId;
            student.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<string> UploadRecommendationLetterAsync(long studentId, IFormFile file, LoggedInUserDto currentUser)
        {
            var student = await _context.KfStudentRegistrations
                .FirstOrDefaultAsync(x => x.StudentId == studentId && x.IsActive);

            if (student == null)
                throw new CustomException("Student not found.");

            if (currentUser.StaffType == StaffType.School &&
                !currentUser.SchoolIds.Contains(student.SchoolId))
            {
                throw new UnauthorizedAccessException();
            }

            if (file == null || file.Length == 0)
                throw new CustomException("Please select a valid recommendation letter.");

            var folder = Path.Combine(
                "wwwroot",
                "uploads",
                "students",
                student.StudentCode,
                "recommendation-letter");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // Delete old letter
            if (!string.IsNullOrWhiteSpace(student.RecommendationLetterPath))
            {
                var oldFile = Path.Combine(Directory.GetCurrentDirectory(),
                    student.RecommendationLetterPath.TrimStart('/', '\\')
                        .Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(oldFile))
                    File.Delete(oldFile);
            }

            var extension = Path.GetExtension(file.FileName);

            var storedFileName =
                $"recommendation_{Guid.NewGuid():N}{extension}";

            var physicalPath = Path.Combine(folder, storedFileName);

            await using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            student.RecommendationLetterPath =
                $"/uploads/students/{student.StudentCode}/recommendation-letter/{storedFileName}";

            student.UpdatedBy = currentUser.LoginId;
            student.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return student.RecommendationLetterPath;
        }

        public async Task<bool> DeleteRecommendationLetterAsync(long studentId, LoggedInUserDto currentUser)
        {
            var student = await _context.KfStudentRegistrations
                .FirstOrDefaultAsync(x => x.StudentId == studentId && x.IsActive);

            if (student == null)
                throw new CustomException("Student not found.");

            if (currentUser.StaffType == StaffType.School &&
                !currentUser.SchoolIds.Contains(student.SchoolId))
            {
                throw new UnauthorizedAccessException();
            }

            if (string.IsNullOrWhiteSpace(student.RecommendationLetterPath))
                return true;

            var physicalPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                student.RecommendationLetterPath.TrimStart('/', '\\')
                    .Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(physicalPath))
                File.Delete(physicalPath);

            student.RecommendationLetterPath = null;
            student.UpdatedBy = currentUser.LoginId;
            student.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }



    }
}
