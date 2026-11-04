using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using ScholarshipManagementAPI.Data.Contexts;
using ScholarshipManagementAPI.Data.DbModels;
using ScholarshipManagementAPI.DTOs.Common.Auth;
using ScholarshipManagementAPI.DTOs.Ngo.SponsorshipAssignments;
using ScholarshipManagementAPI.Helper.Enums;
using ScholarshipManagementAPI.Helper.Utilities;
using ScholarshipManagementAPI.Services.Interface.Ngo;

namespace ScholarshipManagementAPI.Services.Implementation.Ngo;

public sealed class StudentSponsorshipAssignmentService : IStudentSponsorshipAssignmentService
{
    private readonly AppDbContext _context;
    private readonly ILogger<StudentSponsorshipAssignmentService> _logger;

    public StudentSponsorshipAssignmentService(
        AppDbContext context,
        ILogger<StudentSponsorshipAssignmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SponsorshipAssignmentOptionDto>> GetOptionsByCategoryAsync(
        long studentCategoryId,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        EnsureNgoAccess(currentUser);
        if (studentCategoryId <= 0)
            throw new CustomException("A valid student category ID is required.");

        if (!await IsCategoryActiveAsync(studentCategoryId, cancellationToken))
            throw new CustomException("The selected student category is missing or inactive.", StatusCodes.Status404NotFound);

        return await QueryActiveOptions()
            .Where(x => x.StudentCategoryId == studentCategoryId)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.SponsorshipName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SponsorshipAssignmentDto>> GetByApplicationAsync(
        long applicationId,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        if (applicationId <= 0)
            throw new CustomException("A valid application ID is required.");

        var application = await _context.KfStudentProgramApplications
            .AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .Select(x => new { x.ApplicationId, x.Program.UniversityId })
            .FirstOrDefaultAsync(cancellationToken);

        if (application == null)
            throw new CustomException("Application not found.", StatusCodes.Status404NotFound);

        if (currentUser.StaffType != StaffType.Ngo && !currentUser.IsSuperAdmin)
        {
            if (currentUser.StaffType == StaffType.University)
            {
                if (!currentUser.UniversityIds.Contains(application.UniversityId))
                    throw new CustomException("Application not found or access denied.", StatusCodes.Status403Forbidden);
            }
            else
            {
                throw new CustomException("Access denied.", StatusCodes.Status403Forbidden);
            }
        }

        return await BuildAssignmentViewsAsync(applicationId, cancellationToken);
    }

    public async Task<SponsorshipAssignmentDto> CreateAsync(
        long applicationId,
        CreateSponsorshipAssignmentRequestDto request,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        EnsureNgoAccess(currentUser);
        if (applicationId <= 0)
            throw new CustomException("A valid application ID is required.");
        if (request.StudentCategoryId <= 0)
            throw new CustomException("Select a valid student sponsorship category.");

        var remarks = NormalizeRemarks(request.Remarks);
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var application = await _context.KfStudentProgramApplications
                .FirstOrDefaultAsync(x => x.ApplicationId == applicationId, cancellationToken);

            if (application == null)
                throw new CustomException("Application not found.", StatusCodes.Status404NotFound);

            var categoryOptions = await GetValidatedCategoryOptionsAsync(request.StudentCategoryId, cancellationToken);
            var existingCategoryRows = await GetActiveRowsForCategoryAsync(
                applicationId, request.StudentCategoryId, cancellationToken);

            if (existingCategoryRows.Count > 0)
            {
                // Ensure application status is moved to Sponsored if currently Awarded or in review
                if (application.ApplicationStatus == (int)StudentApplicationStatus.Awarded ||
                    application.ApplicationStatus == (int)StudentApplicationStatus.SponsoringInProcess)
                {
                    application.ApplicationStatus = (int)StudentApplicationStatus.Sponsored;
                    application.UpdatedBy = currentUser.LoginId;
                    application.UpdatedDate = DateTime.UtcNow;

                    _context.KfStudentHistories.Add(new KfStudentHistory
                    {
                        StudentId = application.StudentId,
                        ApplicationId = application.ApplicationId,
                        Title = "Application Sponsored",
                        Description = "Direct Aid Committee approved the scholarship sponsorship.",
                        HistoryType = (int)StudentHistoryTypeEnum.ApplicationSponsored,
                        CreatedBy = currentUser.LoginId,
                        CreatedDate = DateTime.UtcNow
                    });

                    await _context.SaveChangesAsync(cancellationToken);
                }

                var existing = await FindCategoryViewAsync(applicationId, request.StudentCategoryId, true, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return existing!;
            }

            var anchor = categoryOptions.OrderBy(x => x.MappingId).First();
            var assignment = new KfStudentSponsorshipAssignment
            {
                ApplicationId = applicationId,
                SponsorshipCategoryMappingId = anchor.MappingId,
                IsActive = true,
                AssignedBy = currentUser.LoginId,
                AssignedDate = DateTime.UtcNow,
                Remarks = remarks
            };

            _context.KfStudentSponsorshipAssignments.Add(assignment);

            // Automatically transition application status from Awarded (or SponsoringInProcess) to Sponsored
            if (application.ApplicationStatus == (int)StudentApplicationStatus.Awarded ||
                application.ApplicationStatus == (int)StudentApplicationStatus.SponsoringInProcess)
            {
                application.ApplicationStatus = (int)StudentApplicationStatus.Sponsored;
                application.UpdatedBy = currentUser.LoginId;
                application.UpdatedDate = DateTime.UtcNow;

                _context.KfStudentHistories.Add(new KfStudentHistory
                {
                    StudentId = application.StudentId,
                    ApplicationId = application.ApplicationId,
                    Title = "Application Sponsored",
                    Description = "Direct Aid Committee approved the scholarship sponsorship.",
                    HistoryType = (int)StudentHistoryTypeEnum.ApplicationSponsored,
                    CreatedBy = currentUser.LoginId,
                    CreatedDate = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
            var result = await FindCategoryViewAsync(applicationId, request.StudentCategoryId, true, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result!;
        }
        catch (Exception operationException)
        {
            await RollbackPreservingOriginalAsync(transaction, operationException);
            throw;
        }
    }

    public async Task<SponsorshipAssignmentDto> AssignDonorAsync(
        AssignDonorSponsorshipRequestDto request,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        EnsureNgoAccess(currentUser);
        if (request.ApplicationId <= 0)
            throw new CustomException("A valid application ID is required.");
        if (request.SponsorshipCategoryId <= 0)
            throw new CustomException("Select a valid student sponsorship category.");

        var donorName = NormalizeDonorString(request.DonorName, 250, "Donor name");
        if (string.IsNullOrWhiteSpace(donorName))
            throw new CustomException("Donor name is required.");

        var donorCode = NormalizeDonorString(request.DonorCode, 100, "Donor code / reference");
        if (string.IsNullOrWhiteSpace(donorCode))
            throw new CustomException("Donor code / reference is required.");

        var remarks = NormalizeRemarks(request.Remarks);

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var application = await _context.KfStudentProgramApplications
                .FirstOrDefaultAsync(x => x.ApplicationId == request.ApplicationId, cancellationToken);

            if (application == null)
                throw new CustomException("Application not found.", StatusCodes.Status404NotFound);

            var categoryOptions = await GetValidatedCategoryOptionsAsync(request.SponsorshipCategoryId, cancellationToken);
            var existingCategoryRows = await GetActiveRowsForCategoryAsync(
                request.ApplicationId, request.SponsorshipCategoryId, cancellationToken);

            var now = DateTime.UtcNow;

            if (existingCategoryRows.Count > 0)
            {
                foreach (var row in existingCategoryRows)
                {
                    row.DonorName = donorName;
                    row.DonorCode = donorCode;
                    if (remarks != null) row.Remarks = remarks;
                    row.UpdatedBy = currentUser.LoginId;
                    row.UpdatedDate = now;
                }
            }
            else
            {
                var anchor = categoryOptions.OrderBy(x => x.MappingId).First();
                var assignment = new KfStudentSponsorshipAssignment
                {
                    ApplicationId = request.ApplicationId,
                    SponsorshipCategoryMappingId = anchor.MappingId,
                    IsActive = true,
                    AssignedBy = currentUser.LoginId,
                    AssignedDate = now,
                    DonorName = donorName,
                    DonorCode = donorCode,
                    Remarks = remarks
                };

                _context.KfStudentSponsorshipAssignments.Add(assignment);
            }

            // Move application status to Sponsored if currently Awarded or in review
            if (application.ApplicationStatus == (int)StudentApplicationStatus.Awarded ||
                application.ApplicationStatus == (int)StudentApplicationStatus.SponsoringInProcess)
            {
                application.ApplicationStatus = (int)StudentApplicationStatus.Sponsored;
                application.UpdatedBy = currentUser.LoginId;
                application.UpdatedDate = now;

                _context.KfStudentHistories.Add(new KfStudentHistory
                {
                    StudentId = application.StudentId,
                    ApplicationId = application.ApplicationId,
                    Title = "Application Sponsored",
                    Description = $"Direct Aid Committee assigned donor-sponsored scholarship category ({donorName} / {donorCode}).",
                    HistoryType = (int)StudentHistoryTypeEnum.ApplicationSponsored,
                    CreatedBy = currentUser.LoginId,
                    CreatedDate = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            var result = await FindCategoryViewAsync(request.ApplicationId, request.SponsorshipCategoryId, true, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result!;
        }
        catch (Exception operationException)
        {
            await RollbackPreservingOriginalAsync(transaction, operationException);
            throw;
        }
    }

    public async Task<SponsorshipAssignmentDto> DeactivateAsync(
        long assignmentId,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        EnsureNgoAccess(currentUser);
        if (assignmentId <= 0)
            throw new CustomException("A valid sponsorship assignment ID is required.");

        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var assignment = await _context.KfStudentSponsorshipAssignments
                .Include(x => x.SponsorshipCategoryMapping)
                .FirstOrDefaultAsync(x => x.AssignmentId == assignmentId, cancellationToken);
            if (assignment == null)
                throw new CustomException("Sponsorship assignment not found.", StatusCodes.Status404NotFound);

            var categoryId = assignment.SponsorshipCategoryMapping.StudentCategoryId;
            var activeRows = assignment.IsActive
                ? await GetActiveRowsForCategoryAsync(assignment.ApplicationId, categoryId, cancellationToken)
                : new List<KfStudentSponsorshipAssignment>();
            if (activeRows.Count > 0)
            {
                var now = DateTime.UtcNow;
                foreach (var row in activeRows)
                {
                    row.IsActive = false;
                    row.UpdatedBy = currentUser.LoginId;
                    row.UpdatedDate = now;
                }
                await _context.SaveChangesAsync(cancellationToken);
            }

            var result = await FindAssignmentViewAsync(assignment.AssignmentId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result!;
        }
        catch (Exception operationException)
        {
            await RollbackPreservingOriginalAsync(transaction, operationException);
            throw;
        }
    }

    public async Task<SponsorshipAssignmentDto> UpdateAsync(
        long assignmentId,
        CreateSponsorshipAssignmentRequestDto request,
        LoggedInUserDto currentUser,
        CancellationToken cancellationToken = default)
    {
        EnsureNgoAccess(currentUser);
        if (assignmentId <= 0)
            throw new CustomException("A valid sponsorship assignment ID is required.");
        if (request.StudentCategoryId <= 0)
            throw new CustomException("Select a valid student sponsorship category.");

        var remarks = NormalizeRemarks(request.Remarks);
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            var assignment = await _context.KfStudentSponsorshipAssignments
                .Include(x => x.SponsorshipCategoryMapping)
                .FirstOrDefaultAsync(x => x.AssignmentId == assignmentId, cancellationToken);
            if (assignment == null)
                throw new CustomException("Sponsorship assignment not found.", StatusCodes.Status404NotFound);
            if (!assignment.IsActive)
                throw new CustomException("Only an active sponsorship assignment can be updated.", StatusCodes.Status409Conflict);

            var categoryOptions = await GetValidatedCategoryOptionsAsync(request.StudentCategoryId, cancellationToken);
            var oldCategoryId = assignment.SponsorshipCategoryMapping.StudentCategoryId;
            var oldCategoryRows = await GetActiveRowsForCategoryAsync(assignment.ApplicationId, oldCategoryId, cancellationToken);
            var now = DateTime.UtcNow;

            if (oldCategoryId == request.StudentCategoryId)
            {
                foreach (var row in oldCategoryRows)
                {
                    row.Remarks = remarks;
                    row.UpdatedBy = currentUser.LoginId;
                    row.UpdatedDate = now;
                }
                await _context.SaveChangesAsync(cancellationToken);
                var updated = await FindCategoryViewAsync(assignment.ApplicationId, oldCategoryId, true, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return updated!;
            }

            if ((await GetActiveRowsForCategoryAsync(assignment.ApplicationId, request.StudentCategoryId, cancellationToken)).Count > 0)
                throw new CustomException("An active assignment already exists for this application and category.", StatusCodes.Status409Conflict);

            foreach (var row in oldCategoryRows)
            {
                row.IsActive = false;
                row.UpdatedBy = currentUser.LoginId;
                row.UpdatedDate = now;
            }

            var anchor = categoryOptions.OrderBy(x => x.MappingId).First();
            var replacement = new KfStudentSponsorshipAssignment
            {
                ApplicationId = assignment.ApplicationId,
                SponsorshipCategoryMappingId = anchor.MappingId,
                IsActive = true,
                AssignedBy = currentUser.LoginId,
                AssignedDate = now,
                Remarks = remarks
            };
            _context.KfStudentSponsorshipAssignments.Add(replacement);

            var application = await _context.KfStudentProgramApplications
                .FirstOrDefaultAsync(x => x.ApplicationId == assignment.ApplicationId, cancellationToken);
            if (application != null && (application.ApplicationStatus == (int)StudentApplicationStatus.Awarded ||
                application.ApplicationStatus == (int)StudentApplicationStatus.SponsoringInProcess))
            {
                application.ApplicationStatus = (int)StudentApplicationStatus.Sponsored;
                application.UpdatedBy = currentUser.LoginId;
                application.UpdatedDate = now;

                _context.KfStudentHistories.Add(new KfStudentHistory
                {
                    StudentId = application.StudentId,
                    ApplicationId = application.ApplicationId,
                    Title = "Application Sponsored",
                    Description = "Direct Aid Committee approved the scholarship sponsorship.",
                    HistoryType = (int)StudentHistoryTypeEnum.ApplicationSponsored,
                    CreatedBy = currentUser.LoginId,
                    CreatedDate = now
                });
            }

            await _context.SaveChangesAsync(cancellationToken);

            var result = await FindCategoryViewAsync(assignment.ApplicationId, request.StudentCategoryId, true, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result!;
        }
        catch (Exception operationException)
        {
            await RollbackPreservingOriginalAsync(transaction, operationException);
            throw;
        }
    }

    private async Task<List<SponsorshipAssignmentDto>> BuildAssignmentViewsAsync(
        long applicationId,
        CancellationToken cancellationToken)
    {
        var rows = await _context.KfStudentSponsorshipAssignments
            .AsNoTracking()
            .Include(x => x.SponsorshipCategoryMapping)
                .ThenInclude(x => x.StudentCategory)
            .Include(x => x.SponsorshipCategoryMapping)
                .ThenInclude(x => x.SponsorshipType)
            .Where(x => x.ApplicationId == applicationId)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.AssignedDate)
            .ThenByDescending(x => x.AssignmentId)
            .ToListAsync(cancellationToken);

        var categoryIds = rows.Select(x => x.SponsorshipCategoryMapping.StudentCategoryId).Distinct().ToList();
        var options = categoryIds.Count == 0
            ? new List<SponsorshipAssignmentOptionDto>()
            : await QueryActiveOptions().Where(x => categoryIds.Contains(x.StudentCategoryId)).ToListAsync(cancellationToken);
        var optionsByCategory = options.GroupBy(x => x.StudentCategoryId)
            .ToDictionary(x => x.Key, x => x.OrderBy(option => option.DisplayOrder).ThenBy(option => option.SponsorshipName).ToList());

        var views = new List<SponsorshipAssignmentDto>();
        foreach (var group in rows.Where(x => x.IsActive).GroupBy(x => x.SponsorshipCategoryMapping.StudentCategoryId))
        {
            var anchor = group.OrderBy(x => x.AssignmentId).First();
            views.Add(ToAssignmentDto(anchor, optionsByCategory));
        }

        views.AddRange(rows.Where(x => !x.IsActive).Select(x => ToAssignmentDto(x, optionsByCategory)));
        return views
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.AssignedDate)
            .ThenByDescending(x => x.AssignmentId)
            .ToList();
    }

    private async Task<SponsorshipAssignmentDto?> FindCategoryViewAsync(
        long applicationId,
        long studentCategoryId,
        bool isActive,
        CancellationToken cancellationToken) =>
        (await BuildAssignmentViewsAsync(applicationId, cancellationToken))
            .FirstOrDefault(x => x.IsActive == isActive && x.StudentCategoryId == studentCategoryId);

    private async Task<SponsorshipAssignmentDto?> FindAssignmentViewAsync(
        long assignmentId,
        CancellationToken cancellationToken)
    {
        var row = await _context.KfStudentSponsorshipAssignments
            .AsNoTracking()
            .Include(x => x.SponsorshipCategoryMapping)
            .FirstOrDefaultAsync(x => x.AssignmentId == assignmentId, cancellationToken);
        if (row == null) return null;

        var views = await BuildAssignmentViewsAsync(row.ApplicationId, cancellationToken);
        return views.FirstOrDefault(x => x.AssignmentId == assignmentId)
            ?? views.FirstOrDefault(x => x.IsActive && x.StudentCategoryId == row.SponsorshipCategoryMapping.StudentCategoryId);
    }

    private SponsorshipAssignmentDto ToAssignmentDto(
        KfStudentSponsorshipAssignment row,
        IReadOnlyDictionary<long, List<SponsorshipAssignmentOptionDto>> optionsByCategory)
    {
        var mapping = row.SponsorshipCategoryMapping;
        return new SponsorshipAssignmentDto
        {
            AssignmentId = row.AssignmentId,
            ApplicationId = row.ApplicationId,
            SponsorshipCategoryMappingId = row.SponsorshipCategoryMappingId,
            SponsorshipTypeId = mapping.SponsorshipTypeId,
            SponsorshipName = mapping.SponsorshipType.SponsorshipName,
            StudentCategoryId = mapping.StudentCategoryId,
            CategoryName = mapping.StudentCategory.CategoryName,
            IsActive = row.IsActive,
            AssignedBy = row.AssignedBy,
            AssignedDate = row.AssignedDate,
            UpdatedBy = row.UpdatedBy,
            UpdatedDate = row.UpdatedDate,
            DonorName = row.DonorName,
            DonorCode = row.DonorCode,
            Remarks = row.Remarks,
            PermittedSponsorshipTypes = optionsByCategory.TryGetValue(mapping.StudentCategoryId, out var permitted)
                ? permitted
                : new List<SponsorshipAssignmentOptionDto>()
        };
    }

    private async Task<List<KfStudentSponsorshipAssignment>> GetActiveRowsForCategoryAsync(
        long applicationId,
        long studentCategoryId,
        CancellationToken cancellationToken) =>
        await _context.KfStudentSponsorshipAssignments
            .Include(x => x.SponsorshipCategoryMapping)
            .Where(x => x.ApplicationId == applicationId
                && x.IsActive
                && x.SponsorshipCategoryMapping.StudentCategoryId == studentCategoryId)
            .ToListAsync(cancellationToken);

    private async Task<List<SponsorshipAssignmentOptionDto>> GetValidatedCategoryOptionsAsync(
        long studentCategoryId,
        CancellationToken cancellationToken)
    {
        if (!await IsCategoryActiveAsync(studentCategoryId, cancellationToken))
            throw new CustomException("The selected student category is missing or inactive.", StatusCodes.Status404NotFound);

        var options = await QueryActiveOptions()
            .Where(x => x.StudentCategoryId == studentCategoryId)
            .OrderBy(x => x.MappingId)
            .ToListAsync(cancellationToken);
        if (options.Count == 0)
            throw new CustomException("The selected category has no active mappings to active sponsorship types.");

        return options;
    }

    private Task<bool> IsCategoryActiveAsync(long studentCategoryId, CancellationToken cancellationToken) =>
        _context.KfSponsorshipStudentCategories
            .AsNoTracking()
            .AnyAsync(x => x.StudentCategoryId == studentCategoryId && x.IsActive, cancellationToken);

    private IQueryable<SponsorshipAssignmentOptionDto> QueryActiveOptions() =>
        _context.KfSponsorshipCategoryMappings
            .AsNoTracking()
            .Where(x => x.IsActive && x.StudentCategory.IsActive && x.SponsorshipType.IsActive)
            .Select(x => new SponsorshipAssignmentOptionDto
            {
                MappingId = x.MappingId,
                StudentCategoryId = x.StudentCategoryId,
                CategoryName = x.StudentCategory.CategoryName,
                SponsorshipTypeId = x.SponsorshipTypeId,
                SponsorshipName = x.SponsorshipType.SponsorshipName,
                FrequencyType = x.SponsorshipType.FrequencyType,
                DisplayOrder = x.SponsorshipType.DisplayOrder
            });

    private static string? NormalizeRemarks(string? remarks)
    {
        var normalized = remarks?.Trim();
        if (normalized?.Length > 500)
            throw new CustomException("Remarks cannot exceed 500 characters.");
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static string? NormalizeDonorString(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > maxLength)
            throw new CustomException($"{fieldName} cannot exceed {maxLength} characters.");
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static void EnsureNgoAccess(LoggedInUserDto currentUser)
    {
        if (currentUser.StaffType != StaffType.Ngo)
            throw new CustomException("Only authorized NGO staff can manage DA Pool sponsorship assignments.", StatusCodes.Status403Forbidden);
        if (currentUser.LoginId <= 0)
            throw new CustomException("The authenticated user identity is invalid.", StatusCodes.Status401Unauthorized);
    }

    private async Task RollbackPreservingOriginalAsync(IDbContextTransaction transaction, Exception operationException)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollbackException)
        {
            _logger.LogError(rollbackException,
                "Rollback failed while processing a sponsorship assignment. The original operation exception is preserved.");
            _logger.LogError(operationException, "Sponsorship assignment operation failed before commit.");
        }
    }
}
