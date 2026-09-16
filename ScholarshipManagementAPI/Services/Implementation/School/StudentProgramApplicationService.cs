using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Common.Response;
using ScholarshipManagementAPI.DTOs.School.StudentProgramApplication;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.School;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ScholarshipManagementAPI.Services.Implementation.School
{
    public class StudentProgramApplicationService : IStudentProgramApplicationService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public StudentProgramApplicationService(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }


        // ======================================================
        // Student Program Application Service Methods
        // via school coordinator (student-facing)
        // ======================================================

        public async Task<List<CandidateProgramResponseDto>> GetCandidateProgramsAsync(long studentId, LoggedInUserDto currentUser)
        {
            var student = await _context.KfStudentRegistrations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.StudentId == studentId && x.IsActive);

            if (student == null)
            {
                throw new CustomException("Student registration not found.");
            }

            if (currentUser.StaffType == StaffType.School)
            {
                if (!currentUser.SchoolIds.Contains(student.SchoolId))
                    throw new UnauthorizedAccessException();
            }

            var activeStatuses = new[]
            {
                (int)StudentApplicationStatus.Draft,
                (int)StudentApplicationStatus.AcceptanceInProcess,
                (int)StudentApplicationStatus.Sponsored,
                (int)StudentApplicationStatus.Awarded,
                (int)StudentApplicationStatus.Registered
            };

            var application = await _context.KfStudentProgramApplications
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.StudentId == studentId &&
                    activeStatuses.Contains(x.ApplicationStatus));

            var programs = await _context.KfPrograms
                .AsNoTracking()
                .Include(p => p.University)
                .Include(p => p.Faculty)
                .Include(p => p.KfProgramDocuments)
                    .ThenInclude(pd => pd.DocumentType)
                .Include(p => p.KfProgramRegistrationWindows)
                .Where(p =>
                    p.IsActive &&
                    p.AccreditationStatus == (int)AccreditationStatusEnum.Accredited)
                .ToListAsync();

            var today = DateTime.UtcNow;
            var targetSemester = GetTargetSemester(student);

            var list = new List<CandidateProgramResponseDto>();
            foreach (var p in programs)
            {
                if (!IsEligible(student, p))
                    continue;

                var currentApplication = (application != null && application.ProgramId == p.ProgramId) ? application : null;

                if (!IsRegistrationOpen(p, targetSemester, today))
                    continue;

                list.Add(new CandidateProgramResponseDto
                {
                    ProgramId = p.ProgramId,
                    ProgramName = p.ProgramName,
                    ProgramCode = p.ProgramCode,
                    UniversityName = p.University.UniversityName,
                    FacultyName = p.Faculty.FacultyName,

                    ApplicationId = currentApplication?.ApplicationId,
                    ApplicationStatus = currentApplication?.ApplicationStatus,
                    ApplicationStatusName = currentApplication != null
                    ? Enum.GetName(typeof(StudentApplicationStatus), currentApplication.ApplicationStatus)
                    : null,

                    RequiredDocuments = p.KfProgramDocuments.Select(pd => new RequiredDocumentDto
                    {
                        ProgramDocumentId = pd.ProgramDocumentId,
                        DocumentTypeId = pd.DocumentTypeId,
                        DocumentTypeName = pd.DocumentType.DocumentName,
                        IsRequired = pd.IsRequired
                    }).ToList()
                });
            }

            return list;
        }

        public async Task<long> ApplyAsync(long studentId, ApplyRequestDto dto, LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.School)
                throw new UnauthorizedAccessException();

            if (!currentUser.SchoolIds.Any())
                throw new UnauthorizedAccessException("User is not associated with any school.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate student
                var student = await _context.KfStudentRegistrations
                    .FirstOrDefaultAsync(x => x.StudentId == studentId && x.IsActive);

                if (student == null)
                {
                    throw new CustomException("Student registration not found.");
                }

                if (!currentUser.SchoolIds.Contains(student.SchoolId))
                    throw new UnauthorizedAccessException();


                // Validate selected program
                var program = await _context.KfPrograms
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.ProgramId == dto.ProgramId &&
                        x.AccreditationStatus == (int)AccreditationStatusEnum.Accredited &&
                        x.IsActive);

                if (program == null)
                {
                    throw new CustomException("Selected program does not exist or is inactive.");
                }

                // TODO:
                // Validate that this program is actually available for this student.
                // (Candidate program/business eligibility validation goes here.)

                // Check whether student already has an active application
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
                        x.StudentId == studentId &&
                        activeStatuses.Contains(x.ApplicationStatus));

                if (hasActiveApplication)
                {
                    throw new CustomException("Student already has an active program application.");
                }

                // Student is no longer a registration draft once a program application is created
                if (student.IsDraft == true)
                {
                    student.IsDraft = false;
                }

                var application = new KfStudentProgramApplication
                {
                    StudentId = studentId,
                    ProgramId = dto.ProgramId,
                    ApplicationStatus = (int)StudentApplicationStatus.Draft,
                    AppliedDate = DateTime.UtcNow,
                    Remarks = dto.Remarks,
                    CreatedBy = currentUser.LoginId,
                    CreatedDate = DateTime.UtcNow
                };

                _context.KfStudentProgramApplications.Add(application);

                await _context.SaveChangesAsync();

                await AddHistoryAsync(
                    studentId: application.StudentId,
                    applicationId: application.ApplicationId,
                    title: GetHistoryTitle(StudentApplicationStatus.Draft),
                    description: GetHistoryDescription(StudentApplicationStatus.Draft),
                    historyType: GetHistoryType(StudentApplicationStatus.Draft),
                    userId: currentUser.LoginId);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return application.ApplicationId;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> CancelApplicationAsync(long applicationId, LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.School)
                throw new UnauthorizedAccessException();

            if (!currentUser.SchoolIds.Any())
                throw new UnauthorizedAccessException("User is not associated with any school.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var app = await _context.KfStudentProgramApplications
                    .Include(a => a.Student)
                    .Include(a => a.Program)
                    .FirstOrDefaultAsync(x => x.ApplicationId == applicationId);

                if (app == null)
                {
                    throw new CustomException("Application not found.");
                }

                if (!currentUser.SchoolIds.Contains(app.Student.SchoolId))
                    throw new UnauthorizedAccessException();

                if (app.ApplicationStatus != (int)StudentApplicationStatus.Draft)
                {
                    throw new CustomException("Only Draft applications can be cancelled.");
                }

                // Delete uploaded files
                var documents = await _context.KfStudentProgramDocuments
                    .Where(x => x.ApplicationId == applicationId)
                    .ToListAsync();

                foreach (var document in documents)
                {
                    if (File.Exists(document.StoragePath))
                    {
                        try
                        {
                            File.Delete(document.StoragePath);
                        }
                        catch
                        {
                            // Optional: Log exception
                        }
                    }
                }

                // History
                await AddHistoryAsync(
                    studentId: app.StudentId,
                    applicationId: app.ApplicationId,
                    title: "Application Draft Cancelled",
                    description: $"Cancelled draft application for program '{app.Program.ProgramName}'.",
                    historyType: StudentHistoryTypeEnum.ApplicationDraftCancelled,
                    userId: currentUser.LoginId);


                // Delete application (documents will be deleted by cascade)
                _context.KfStudentProgramApplications.Remove(app);

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

        public async Task<bool> SubmitApplicationAsync(long applicationId, LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.School)
                throw new UnauthorizedAccessException();

            if (!currentUser.SchoolIds.Any())
                throw new UnauthorizedAccessException("User is not associated with any school.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var app = await _context.KfStudentProgramApplications
                    .Include(a => a.Student)
                    .Include(a => a.Program)
                        .ThenInclude(p => p.KfProgramDocuments)
                    .Include(a => a.KfStudentProgramDocuments)
                    .FirstOrDefaultAsync(x => x.ApplicationId == applicationId);

                if (app == null)
                {
                    throw new CustomException("Application not found.");
                }

                if (!currentUser.SchoolIds.Contains(app.Student.SchoolId))
                    throw new UnauthorizedAccessException();

                if (app.ApplicationStatus != (int)StudentApplicationStatus.Draft)
                {
                    throw new CustomException("Only Draft applications can be submitted.");
                }

                // Validate required documents
                var requiredDocuments = app.Program.KfProgramDocuments
                    .Where(x => x.IsRequired)
                    .Select(x => x.ProgramDocumentId)
                    .ToList();

                var uploadedDocuments = app.KfStudentProgramDocuments
                    .Select(x => x.ProgramDocumentId)
                    .ToList();

                var missingDocuments = requiredDocuments
                    .Except(uploadedDocuments)
                    .ToList();

                if (missingDocuments.Any())
                {
                    throw new CustomException("Please upload all required documents before submitting the application.");
                }

                app.ApplicationStatus = (int)StudentApplicationStatus.AcceptanceInProcess;
                app.SubmittedDate = DateTime.UtcNow;
                app.UpdatedBy = currentUser.LoginId;
                app.UpdatedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();


                await AddHistoryAsync(
                    studentId: app.StudentId,
                    applicationId: app.ApplicationId,
                    title: GetHistoryTitle(StudentApplicationStatus.AcceptanceInProcess),
                    description: GetHistoryDescription(StudentApplicationStatus.AcceptanceInProcess),
                    historyType: GetHistoryType(StudentApplicationStatus.AcceptanceInProcess),
                    userId: currentUser.LoginId);

                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<StudentProgramApplicationResponseDto?> GetApplicationAsync(long applicationId, LoggedInUserDto currentUser)
        {
            var app = await _context.KfStudentProgramApplications
                .AsNoTracking()
                .Include(a => a.Student)
                .Include(a => a.Program)
                    .ThenInclude(p => p.University)
                .Include(a => a.Program)
                    .ThenInclude(p => p.Faculty)
                .Include(a => a.Program)
                    .ThenInclude(p => p.KfProgramDocuments)
                        .ThenInclude(pd => pd.DocumentType)
                .Include(a => a.KfStudentProgramDocuments)
                    .ThenInclude(d => d.DocumentType)
                .FirstOrDefaultAsync(x => x.ApplicationId == applicationId);

            if (app == null)
            {
                return null;
            }

            if (currentUser.StaffType == StaffType.School)
            {
                if (!currentUser.SchoolIds.Contains(app.Student.SchoolId))
                    throw new UnauthorizedAccessException();
            }

            return new StudentProgramApplicationResponseDto
            {
                ApplicationId = app.ApplicationId,
                StudentId = app.StudentId,

                ProgramId = app.ProgramId,
                ProgramName = app.Program.ProgramName,
                ProgramCode = app.Program.ProgramCode,

                UniversityName = app.Program.University.UniversityName,
                FacultyName = app.Program.Faculty.FacultyName,

                ApplicationStatus = app.ApplicationStatus,
                ApplicationStatusName = Enum.GetName(typeof(StudentApplicationStatus), app.ApplicationStatus)
                                        ?? app.ApplicationStatus.ToString(),

                AppliedDate = app.AppliedDate,
                SubmittedDate = app.SubmittedDate,

                Remarks = app.Remarks,

                CreatedBy = app.CreatedBy,
                CreatedDate = app.CreatedDate,

                RequiredDocuments = app.Program.KfProgramDocuments
                    .Select(pd => new RequiredDocumentDto
                    {
                        ProgramDocumentId = pd.ProgramDocumentId,
                        DocumentTypeId = pd.DocumentTypeId,
                        DocumentTypeName = pd.DocumentType.DocumentName,
                        IsRequired = pd.IsRequired
                    })
                    .ToList(),

                Documents = app.KfStudentProgramDocuments
                    .Select(d => new StudentProgramDocumentResponseDto
                    {
                        StudentProgramDocumentId = d.StudentProgramDocumentId,
                        ApplicationId = d.ApplicationId,
                        ProgramDocumentId = d.ProgramDocumentId,
                        DocumentTypeId = d.DocumentTypeId,
                        DocumentTypeName = d.DocumentType.DocumentName,
                        OriginalFileName = d.OriginalFileName,
                        StoredFileName = d.StoredFileName,
                        StoragePath = d.StoragePath,
                        ContentType = d.ContentType,
                        FileSize = d.FileSize,
                        ReviewerRemark = d.ReviewerRemark,
                        UploadedBy = d.UploadedBy,
                        UploadedDate = d.UploadedDate,

                        IsRequired = app.Program.KfProgramDocuments
                            .Any(pd =>
                                pd.ProgramDocumentId == d.ProgramDocumentId &&
                                pd.IsRequired)
                    })
                    .ToList()
            };
        }

        public async Task<StudentProgramDocumentResponseDto> UploadDocumentAsync(long applicationId, long programDocumentId,
            long documentTypeId, IFormFile file, LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.School)
                throw new UnauthorizedAccessException();

            if (!currentUser.SchoolIds.Any())
                throw new UnauthorizedAccessException("User is not associated with any school.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Validate Application
                var app = await _context.KfStudentProgramApplications
                    .Include(x => x.Student)
                    .Include(x => x.Program)
                    .FirstOrDefaultAsync(x => x.ApplicationId == applicationId);

                if (app == null)
                {
                    throw new CustomException("Application not found.");
                }

                if (!currentUser.SchoolIds.Contains(app.Student.SchoolId))
                    throw new UnauthorizedAccessException();

                // Only Draft applications allow document upload
                if (app.ApplicationStatus != (int)StudentApplicationStatus.Draft)
                {
                    throw new CustomException("Documents can only be uploaded while the application is in Draft status.");
                }

                // Validate File
                if (file == null || file.Length == 0)
                {
                    throw new CustomException("Please select a valid document.");
                }

                // Validate Program Document
                var programDocument = await _context.KfProgramDocuments
                    .Include(x => x.DocumentType)
                    .FirstOrDefaultAsync(x =>
                        x.ProgramDocumentId == programDocumentId &&
                        x.ProgramId == app.ProgramId);

                if (programDocument == null)
                {
                    throw new CustomException("Invalid program document.");
                }

                // Validate Document Type
                if (programDocument.DocumentTypeId != documentTypeId)
                {
                    throw new CustomException("Invalid document type.");
                }

                // Prevent Duplicate Upload
                bool alreadyUploaded = await _context.KfStudentProgramDocuments
                    .AnyAsync(x =>
                        x.ApplicationId == applicationId &&
                        x.ProgramDocumentId == programDocumentId);

                if (alreadyUploaded)
                {
                    throw new CustomException(
                        $"{programDocument.DocumentType.DocumentName} has already been uploaded.");
                }

                // Create Upload Folder
                var folder = Path.Combine(
                    _environment.WebRootPath,
                    "uploads",
                    "student-applications",
                    applicationId.ToString());

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                // Generate File Name
                var extension = Path.GetExtension(file.FileName);

                var storedFileName =
                    $"{Guid.NewGuid():N}{extension}";

                
                var physicalPath = Path.Combine(folder, storedFileName);

                var relativePath =
                    $"/uploads/student-applications/{applicationId}/{storedFileName}";

                // Save File
                await using (var stream = new FileStream(physicalPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var document = new KfStudentProgramDocument
                {
                    ApplicationId = applicationId,
                    ProgramDocumentId = programDocumentId,
                    DocumentTypeId = documentTypeId,

                    OriginalFileName = file.FileName,
                    StoredFileName = storedFileName,
                    StoragePath = relativePath,

                    ContentType = file.ContentType,
                    FileSize = file.Length,

                    UploadedBy = currentUser.LoginId,
                    UploadedDate = DateTime.UtcNow
                };

                _context.KfStudentProgramDocuments.Add(document);

                await AddHistoryAsync(
                    studentId: app.StudentId,
                    applicationId: app.ApplicationId,
                    title: "Document Uploaded",
                    description: $"Uploaded '{programDocument.DocumentType.DocumentName}'.",
                    historyType: StudentHistoryTypeEnum.DocumentUploaded,
                    userId: currentUser.LoginId);

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return new StudentProgramDocumentResponseDto
                {
                    StudentProgramDocumentId = document.StudentProgramDocumentId,
                    ApplicationId = document.ApplicationId,

                    ProgramDocumentId = document.ProgramDocumentId,

                    DocumentTypeId = document.DocumentTypeId,
                    DocumentTypeName = programDocument.DocumentType.DocumentName,

                    OriginalFileName = document.OriginalFileName,
                    StoredFileName = document.StoredFileName,
                    StoragePath = document.StoragePath,

                    ContentType = document.ContentType,
                    FileSize = document.FileSize,

                    UploadedBy = document.UploadedBy,
                    UploadedDate = document.UploadedDate,

                    IsRequired = programDocument.IsRequired
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> DeleteDocumentAsync(long applicationId, long documentId, LoggedInUserDto currentUser)
        {
            if (currentUser.StaffType != StaffType.School)
                throw new UnauthorizedAccessException();

            if (!currentUser.SchoolIds.Any())
                throw new UnauthorizedAccessException("User is not associated with any school.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var app = await _context.KfStudentProgramApplications
                    .Include(x => x.Student)
                    .FirstOrDefaultAsync(x => x.ApplicationId == applicationId);

                if (app == null)
                {
                    throw new CustomException("Application not found.");
                }

                if (!currentUser.SchoolIds.Contains(app.Student.SchoolId))
                    throw new UnauthorizedAccessException();

                if (app.ApplicationStatus != (int)StudentApplicationStatus.Draft)
                {
                    throw new CustomException("Documents can only be deleted while the application is in Draft status.");
                }

                var document = await _context.KfStudentProgramDocuments
                    .Include(x => x.DocumentType)
                    .FirstOrDefaultAsync(x =>
                        x.StudentProgramDocumentId == documentId &&
                        x.ApplicationId == applicationId);

                if (document == null)
                {
                    throw new CustomException("Document not found.");
                }

                // Delete physical file

                var physicalPath = Path.Combine(
                    _environment.WebRootPath,
                    document.StoragePath.TrimStart('/')
                    .Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(physicalPath))
                {
                    try
                    {
                        File.Delete(physicalPath);
                    }
                    catch
                    {
                        // Optional: Log exception
                    }
                }

                _context.KfStudentProgramDocuments.Remove(document);

                await AddHistoryAsync(
                    studentId: app.StudentId,
                    applicationId: app.ApplicationId,
                    title: "Document Deleted",
                    description: $"Deleted '{document.DocumentType.DocumentName}'.",
                    historyType: StudentHistoryTypeEnum.DocumentDeleted,
                    userId: currentUser.LoginId);

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

        public async Task<List<StudentProgramDocumentResponseDto>> GetDocumentsAsync(long applicationId, LoggedInUserDto currentUser)
        {
            var application = await _context.KfStudentProgramApplications
                .AsNoTracking()
                .Include(x => x.Student)
                .FirstOrDefaultAsync(x => x.ApplicationId == applicationId);

            if (application == null)
            {
                throw new CustomException("Application not found.");
            }

            if (currentUser.StaffType == StaffType.School)
            {
                if (!currentUser.SchoolIds.Contains(application.Student.SchoolId))
                    throw new UnauthorizedAccessException();
            }

            return await _context.KfStudentProgramDocuments
                .AsNoTracking()
                .Include(d => d.DocumentType)
                .Include(d => d.ProgramDocument)
                .Where(x => x.ApplicationId == applicationId)
                .Select(d => new StudentProgramDocumentResponseDto
                {
                    StudentProgramDocumentId = d.StudentProgramDocumentId,
                    ApplicationId = d.ApplicationId,
                    ProgramDocumentId = d.ProgramDocumentId,

                    DocumentTypeId = d.DocumentTypeId,
                    DocumentTypeName = d.DocumentType.DocumentName,

                    OriginalFileName = d.OriginalFileName,
                    StoredFileName = d.StoredFileName,
                    StoragePath = d.StoragePath,

                    ContentType = d.ContentType,
                    FileSize = d.FileSize,

                    ReviewerRemark = d.ReviewerRemark,

                    UploadedBy = d.UploadedBy,
                    UploadedDate = d.UploadedDate,

                    IsRequired = d.ProgramDocument.IsRequired
                })
                .ToListAsync();
        }


        public async Task<List<StudentHistoryResponseDto>> GetHistoryAsync(long studentId, LoggedInUserDto currentUser)
        {
            var studentExists = await _context.KfStudentRegistrations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.StudentId == studentId && x.IsActive);

            if (studentExists == null)
            {
                throw new CustomException("Student not found.");
            }

            if (currentUser.StaffType == StaffType.School)
            {
                if (!currentUser.SchoolIds.Contains(studentExists.SchoolId))
                    throw new UnauthorizedAccessException();
            }

            return await _context.KfStudentHistories
                .AsNoTracking()
                .Where(x => x.StudentId == studentId)
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new StudentHistoryResponseDto
                {
                    StudentHistoryId = x.StudentHistoryId,
                    StudentId = x.StudentId,
                    ApplicationId = x.ApplicationId,

                    Title = x.Title,
                    Description = x.Description,

                    HistoryType = x.HistoryType,

                    CreatedBy = x.CreatedBy,
                    CreatedDate = x.CreatedDate
                })
                .ToListAsync();
        }





        // ======================================================
        // Search Student Program Application and change status 
        // via University or NGO staff
        // ======================================================

        public async Task<PagedResultDto<StudentProgramApplicationDto>> SearchAsync(StudentProgramApplicationFilterDto filter, LoggedInUserDto currentUser)
        {
            var query = _context.KfStudentProgramApplications
                .AsNoTracking()
                .AsQueryable();


            // Role filter
            switch (currentUser.StaffType)
            {
                case StaffType.University:
                    {
                        if (!currentUser.UniversityIds.Any())
                            throw new UnauthorizedAccessException("User is not associated with a university.");

                        query = query.Where(x =>
                            currentUser.UniversityIds.Contains(x.Program.UniversityId) &&
                            x.ApplicationStatus >= (int)StudentApplicationStatus.AcceptanceInProcess);

                        break;
                    }

                case StaffType.Ngo:
                    {
                        // NGO should only see applications after they have been awarded
                        //query = query.Where(x =>
                        //    x.ApplicationStatus >= (int)StudentApplicationStatus.Awarded &&
                        //    x.ApplicationStatus <= (int)StudentApplicationStatus.Sponsored);

                        query = query.Where(x =>
                            x.ApplicationStatus >= (int)StudentApplicationStatus.Awarded );

                        break;
                    }
            }

            query = query.Where(x => x.ApplicationStatus != (int)StudentApplicationStatus.Draft);

            if (filter.SchoolCoordinatorId.HasValue)
            {
                query = query.Where(x => x.CreatedBy == filter.SchoolCoordinatorId);
            }

            if (filter.CountryId.HasValue)
                query = query.Where(x => x.Program.University.CountryId == filter.CountryId);

            if (filter.UniversityId.HasValue)
                query = query.Where(x => x.Program.UniversityId == filter.UniversityId);

            if (filter.FacultyId.HasValue)
                query = query.Where(x => x.Program.FacultyId == filter.FacultyId);

            if (filter.ProgramId.HasValue)
                query = query.Where(x => x.ProgramId == filter.ProgramId);

            if (filter.ApplicationStatusId.HasValue)
                query = query.Where(x => x.ApplicationStatus == filter.ApplicationStatusId);

            if (!string.IsNullOrWhiteSpace(filter.SearchText))
            {
                var search = filter.SearchText.Trim();

                query = query.Where(x =>
                    x.Student.FirstName.Contains(search) ||
                    (x.Student.SecondName != null && x.Student.SecondName.Contains(search)) ||
                    (x.Student.ThirdName != null && x.Student.ThirdName.Contains(search)) ||
                    x.Student.LastName.Contains(search) ||
                    (x.Student.Email != null && x.Student.Email.Contains(search)) ||
                    (x.Student.Phone != null && x.Student.Phone.Contains(search)) ||
                    x.Student.StudentCode.Contains(search) ||
                    x.Program.ProgramName.Contains(search) ||
                    x.Program.ProgramCode.Contains(search) ||
                    x.Program.Faculty.FacultyName.Contains(search));
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
                .Select(x => new StudentProgramApplicationDto
                {
                    StudentId = x.StudentId,
                    StudentCode = x.Student.StudentCode,
                    PhotoPath = x.Student.PhotoPath,

                    FirstName = x.Student.FirstName,
                    SecondName = x.Student.SecondName,
                    ThirdName = x.Student.ThirdName,
                    LastName = x.Student.LastName,

                    SchoolId = x.Student.SchoolId,
                    SchoolName = x.Student.School != null ? x.Student.School.SchoolName : null,

                    HighSchoolTotalScore = x.Student.TotalScore,
                    HighSchoolMaxScore = x.Student.MaxScore,
                    HighSchoolRelativeGradeOrPercentage = x.Student.RelativeGrade,
                    EnglishScore = x.Student.EnglishScore,
                    HsSpecialization = x.Student.HsSpecialization,
                    TanzanianStudentCombination = x.Student.TanzanianStudentCombination,

                    ApplicationId = x.ApplicationId,
                    ApplicationStatusId = x.ApplicationStatus,
                    ApplicationStatusName = ((StudentApplicationStatus)x.ApplicationStatus).ToString(),
                    ActionDate = x.UpdatedDate ?? x.SubmittedDate ?? x.AppliedDate,

                    ProgramId = x.ProgramId,
                    ProgramName = x.Program.ProgramName,
                    ProgramCode = x.Program.ProgramCode,

                    FacultyId = x.Program.FacultyId,
                    FacultyName = x.Program.Faculty.FacultyName,

                    UniversityId = x.Program.UniversityId,
                    UniversityName = x.Program.University.UniversityName,

                    UniversityCountryId = x.Program.University.CountryId,
                    UniversityCountryName = x.Program.University.Country != null ? x.Program.University.Country.CountryName : null
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
                    }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            return new PagedResultDto<StudentProgramApplicationDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = filter.PageNumber,
                PageSize = filter.PageSize
            };
        }


        public async Task<StudentProgramApplicationDto?> GetByIdAsync(long applicationId, LoggedInUserDto currentUser)
        {
            var query = _context.KfStudentProgramApplications
                .AsNoTracking()
                .Where(x => x.ApplicationId == applicationId);

            switch (currentUser.StaffType)
            {
                case StaffType.University:

                    if (!currentUser.UniversityIds.Any())
                        throw new UnauthorizedAccessException("User is not associated with a university.");

                    query = query.Where(x =>
                            currentUser.UniversityIds.Contains(x.Program.UniversityId) &&
                            x.ApplicationStatus >= (int)StudentApplicationStatus.AcceptanceInProcess);

                    break;

                case StaffType.Ngo:

                    //query = query.Where(x =>
                    //        x.ApplicationStatus >= (int)StudentApplicationStatus.Awarded &&
                    //        x.ApplicationStatus <= (int)StudentApplicationStatus.Sponsored);

                    query = query.Where(x =>
                            x.ApplicationStatus >= (int)StudentApplicationStatus.Awarded);

                    break;
            }

            var item = await query
                .Select(x => new StudentProgramApplicationDto
                {
                    StudentId = x.StudentId,
                    StudentCode = x.Student.StudentCode,
                    PhotoPath = x.Student.PhotoPath,

                    FirstName = x.Student.FirstName,
                    SecondName = x.Student.SecondName,
                    ThirdName = x.Student.ThirdName,
                    LastName = x.Student.LastName,

                    MotherName = x.Student.MotherName,
                    DateOfBirth = x.Student.Dob.HasValue
                        ? x.Student.Dob.Value.ToDateTime(TimeOnly.MinValue)
                        : null,

                    GenderId = x.Student.GenderId,
                    GenderName = x.Student.Gender != null ? x.Student.Gender.DisplayText : null,

                    ReligionId = x.Student.ReligionId,
                    ReligionName = x.Student.Religion != null ? x.Student.Religion.DisplayText : null,

                    Nationality = x.Student.Nationality != null ? x.Student.Nationality.CountryName : null,
                    CountryOfResidence = x.Student.ResidenceCountry != null ? x.Student.ResidenceCountry.CountryName : null,

                    IsDirectAidOrphan = x.Student.IsOrphan,
                    OrphanNumber = x.Student.OrphanNumber,

                    PhoneNumber = x.Student.Phone,
                    EmailAddress = x.Student.Email,

                    City = x.Student.City,
                    Village = x.Student.Village,
                    Block = x.Student.Block,
                    Street = x.Student.Street,

                    SchoolId = x.Student.SchoolId,
                    SchoolName = x.Student.School != null ? x.Student.School.SchoolName : null,

                    HighSchoolTotalScore = x.Student.TotalScore,
                    HighSchoolMaxScore = x.Student.MaxScore,
                    HighSchoolRelativeGradeOrPercentage = x.Student.RelativeGrade,
                    EnglishScore = x.Student.EnglishScore,
                    HsSpecialization = x.Student.HsSpecialization,
                    TanzanianStudentCombination = x.Student.TanzanianStudentCombination,

                    ApplicationId = x.ApplicationId,
                    ApplicationStatusId = x.ApplicationStatus,
                    ApplicationStatusName = ((StudentApplicationStatus)x.ApplicationStatus).ToString(),
                    ActionDate = x.UpdatedDate ?? x.SubmittedDate ?? x.AppliedDate,

                    ProgramId = x.ProgramId,
                    ProgramName = x.Program.ProgramName,
                    ProgramCode = x.Program.ProgramCode,

                    FacultyId = x.Program.FacultyId,
                    FacultyName = x.Program.Faculty.FacultyName,

                    UniversityId = x.Program.UniversityId,
                    UniversityName = x.Program.University.UniversityName
                })
                .FirstOrDefaultAsync();

            if (item == null)
                return null;

            item.FullName = string.Join(" ",
                new[]
                {
            item.FirstName,
            item.SecondName,
            item.ThirdName,
            item.LastName
                }.Where(s => !string.IsNullOrWhiteSpace(s)));

            return item;
        }


        public async Task<bool> ChangeStatusAsync(long applicationId, ChangeStudentProgramStatusDto dto, LoggedInUserDto currentUser)
        {
            var application = await _context.KfStudentProgramApplications
                .Include(x => x.Student)
                .Include(x => x.Program)
                .FirstOrDefaultAsync(x => x.ApplicationId == applicationId);

            if (application == null)
                throw new CustomException("Application not found.");

            ValidateApplicationAccess(application, currentUser);

            if (!Enum.IsDefined(typeof(StudentApplicationStatus), dto.ApplicationStatusId))
                throw new CustomException("Invalid application status.");

            var currentStatus = (StudentApplicationStatus)application.ApplicationStatus;
            var newStatus = (StudentApplicationStatus)dto.ApplicationStatusId;

            if (currentStatus == newStatus)
                throw new CustomException("Application is already in the selected status.");

            if (!CanChangeStatus(currentStatus, newStatus, currentUser))
                throw new CustomException("Invalid application status transition.");

            application.ApplicationStatus = dto.ApplicationStatusId;

            if (!string.IsNullOrWhiteSpace(dto.Remarks))
                application.Remarks = dto.Remarks.Trim();

            application.UpdatedBy = currentUser.LoginId;
            application.UpdatedDate = DateTime.UtcNow;

            await AddHistoryAsync(
                application.StudentId,
                application.ApplicationId,
                GetHistoryTitle(newStatus),
                GetHistoryDescription(newStatus),
                GetHistoryType(newStatus),
                currentUser.LoginId);

            await _context.SaveChangesAsync();

            return true;
        }





        // ======================================================
        // Private Helper Methods for History Generation
        // ======================================================

        private StudentHistoryTypeEnum GetHistoryType(StudentApplicationStatus status)
        {
            return status switch
            {
                StudentApplicationStatus.Draft => StudentHistoryTypeEnum.ApplicationDraftCreated,
                StudentApplicationStatus.AcceptanceInProcess => StudentHistoryTypeEnum.ApplicationSubmittedForReview,

                StudentApplicationStatus.Accepted => StudentHistoryTypeEnum.ApplicationAccepted,
                StudentApplicationStatus.AcceptanceRejected => StudentHistoryTypeEnum.ApplicationAcceptanceRejected,

                StudentApplicationStatus.AwardingInProcess => StudentHistoryTypeEnum.ApplicationAwardingInProcess,
                StudentApplicationStatus.Awarded => StudentHistoryTypeEnum.ApplicationAwarded,
                StudentApplicationStatus.AwardingRejected => StudentHistoryTypeEnum.ApplicationAwardingRejected,

                StudentApplicationStatus.SponsoringInProcess => StudentHistoryTypeEnum.ApplicationSponsoringInProcess,
                StudentApplicationStatus.Sponsored => StudentHistoryTypeEnum.ApplicationSponsored,
                StudentApplicationStatus.SponsoringRejected => StudentHistoryTypeEnum.ApplicationSponsoringRejected,

                StudentApplicationStatus.Registered => StudentHistoryTypeEnum.StudentRegistered,
                StudentApplicationStatus.Graduated => StudentHistoryTypeEnum.StudentGraduated,

                StudentApplicationStatus.Failed => StudentHistoryTypeEnum.StudentFailed,

                StudentApplicationStatus.Dismissed => StudentHistoryTypeEnum.StudentDismissed,

                _ => StudentHistoryTypeEnum.ApplicationUpdated
            };
        }

        private string GetHistoryTitle(StudentApplicationStatus status)
        {
            return status switch
            {
                StudentApplicationStatus.Draft => "Application Draft Created",
                StudentApplicationStatus.AcceptanceInProcess => "Application Submitted",

                StudentApplicationStatus.Accepted => "Application Accepted",
                StudentApplicationStatus.AcceptanceRejected => "Application Rejected",

                StudentApplicationStatus.AwardingInProcess => "Awarding Started",
                StudentApplicationStatus.Awarded => "Application Awarded",
                StudentApplicationStatus.AwardingRejected => "Awarding Rejected",

                StudentApplicationStatus.SponsoringInProcess => "Sponsoring Started",
                StudentApplicationStatus.Sponsored => "Application Sponsored",
                StudentApplicationStatus.SponsoringRejected => "Sponsoring Rejected",

                StudentApplicationStatus.Registered => "Student Registered",
                StudentApplicationStatus.Failed => "Student Failed",
                StudentApplicationStatus.Dismissed => "Student Dismissed",
                StudentApplicationStatus.Graduated => "Student Graduated",

                _ => "Application Updated"
            };
        }

        private string GetHistoryDescription(StudentApplicationStatus status)
        {
            return status switch
            {
                StudentApplicationStatus.Draft  => 
                    "Student started a new program application.",

                StudentApplicationStatus.Accepted =>
                    "University accepted the student's application.",

                StudentApplicationStatus.AcceptanceInProcess =>
                    "Application submitted successfully and is awaiting university document review.",

                StudentApplicationStatus.AcceptanceRejected =>
                    "University rejected the student's application.",

                StudentApplicationStatus.AwardingInProcess =>
                    "University started the awarding review process.",

                StudentApplicationStatus.Awarded =>
                    "University completed the awarding process.",

                StudentApplicationStatus.AwardingRejected =>
                    "University rejected the application during awarding review.",

                StudentApplicationStatus.SponsoringInProcess =>
                    "Direct Aid Committee started sponsorship review.",

                StudentApplicationStatus.Sponsored =>
                    "Direct Aid Committee approved the scholarship sponsorship.",

                StudentApplicationStatus.SponsoringRejected =>
                    "Direct Aid Committee rejected the scholarship sponsorship.",

                StudentApplicationStatus.Registered =>
                    "Student registered successfully.",

                StudentApplicationStatus.Graduated =>
                    "Student graduated successfully.",

                StudentApplicationStatus.Failed =>
                    "Student was marked as failed.",

                StudentApplicationStatus.Dismissed =>
                    "Student was dismissed from the program.",

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




        // ======================================================
        // Validation (who can access and change application status)
        // ======================================================

        private static void ValidateApplicationAccess(KfStudentProgramApplication application, LoggedInUserDto currentUser)
        {
            switch (currentUser.StaffType)
            {
                case StaffType.School:
                    if (!currentUser.SchoolIds.Contains(application.Student.SchoolId))
                        throw new UnauthorizedAccessException(
                            "You are not authorized to access this student's application.");
                    break;

                case StaffType.University:

                    if (!currentUser.UniversityIds.Any())
                        throw new UnauthorizedAccessException("User is not associated with a university.");

                    if (!currentUser.UniversityIds.Contains(application.Program.UniversityId))
                        throw new UnauthorizedAccessException(
                            "You are not authorized to access this application's university.");
                    break;

                case StaffType.Ngo:
                    // NGO users can access all sponsored applications.
                    break;

                default:
                    throw new UnauthorizedAccessException();
            }
        }


        private bool CanChangeStatus(StudentApplicationStatus currentStatus,
            StudentApplicationStatus newStatus, LoggedInUserDto currentUser)
        {
            switch (currentUser.StaffType)
            {
                // ======================================================
                // University
                // ======================================================
                case StaffType.University:

                    switch (currentStatus)
                    {
                        // Acceptance Review
                        case StudentApplicationStatus.AcceptanceInProcess:
                            return newStatus == StudentApplicationStatus.Accepted ||
                                   newStatus == StudentApplicationStatus.AcceptanceRejected;

                        // Start Awarding Review
                        case StudentApplicationStatus.Accepted:
                            return newStatus == StudentApplicationStatus.AwardingInProcess;

                        // Awarding Review
                        case StudentApplicationStatus.AwardingInProcess:
                            return newStatus == StudentApplicationStatus.Awarded ||
                                   newStatus == StudentApplicationStatus.AwardingRejected;

                        default:
                            return false;
                    }


                // ======================================================
                // Direct Aid Committee (NGO)
                // ======================================================
                case StaffType.Ngo:

                    switch (currentStatus)
                    {
                        // Start Sponsoring Review
                        case StudentApplicationStatus.Awarded:
                            return newStatus == StudentApplicationStatus.SponsoringInProcess;

                        // Sponsoring Review
                        case StudentApplicationStatus.SponsoringInProcess:
                            return newStatus == StudentApplicationStatus.Sponsored ||
                                   newStatus == StudentApplicationStatus.SponsoringRejected;

                        default:
                            return false;
                    }

                default:
                    return false;
            }
        }



        // ======================================================
        // Eligibility Check
        // ======================================================

        private bool IsEligible(KfStudentRegistration student, KfProgram program)
        {
            // Minimum Percentage
            if (program.MinAcceptanceRate.HasValue &&
                student.RelativeGrade < program.MinAcceptanceRate)
            {
                return false;
            }

            // High School Division
            if (program.AllowedHighSchoolDivisions != null)
            {
                var allowedDivisions = program.AllowedHighSchoolDivisions
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim());

                if (!allowedDivisions.Contains(student.HsSpecialization))
                    return false;
            }

            // Tanzanian Combination
            if (program.AllowedTanzanianCombinations != null)
            {
                var allowedCombinations = program.AllowedTanzanianCombinations
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim());

                if (!allowedCombinations.Contains(student.TanzanianStudentCombination))
                    return false;
            }

            return true;
        }


        private bool IsRegistrationOpen(KfProgram program, int semesterNo, DateTime currentDate)
        {
            var result = program.KfProgramRegistrationWindows.Any(r =>
                r.SemesterNo == semesterNo &&
                r.RegistrationFrom <= currentDate &&
                r.RegistrationTo >= currentDate);

            return result;
        }


        public static int GetTargetSemester(KfStudentRegistration student)
        {
            // Future:
            // if (student.IsTransferStudent)
            //     return student.TransferSemester;

            // Normal admission
            return 1;
        }


    }
}
