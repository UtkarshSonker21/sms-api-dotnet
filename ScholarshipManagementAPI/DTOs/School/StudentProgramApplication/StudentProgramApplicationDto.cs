using System;

namespace ScholarshipManagementAPI.DTOs.School.StudentProgramApplication;


public class StudentProgramApplicationDto
{
    // Student
    public long StudentId { get; set; }
    public string? StudentCode { get; set; }
    public string? PhotoPath { get; set; }

    public string? FirstName { get; set; }
    public string? SecondName { get; set; }
    public string? ThirdName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }

    // Personal Information
    public string? MotherName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public long? GenderId { get; set; }
    public string? GenderName { get; set; }
    public long? ReligionId { get; set; }
    public string? ReligionName { get; set; }
    public string? Nationality { get; set; }
    public string? CountryOfResidence { get; set; }
    public bool? IsDirectAidOrphan { get; set; }
    public string? OrphanNumber { get; set; }

    // Contact
    public string? PhoneNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? City { get; set; }
    public string? Village { get; set; }
    public string? Block { get; set; }
    public string? Street { get; set; }

    // Academic Information
    public decimal? HighSchoolTotalScore { get; set; }
    public decimal? HighSchoolMaxScore { get; set; }
    public decimal? HighSchoolRelativeGradeOrPercentage { get; set; }
    public decimal? EnglishScore { get; set; }
    public string? HsSpecialization { get; set; }
    public string? TanzanianStudentCombination { get; set; }

    // School
    public long? SchoolId { get; set; }
    public string? SchoolName { get; set; }

    // Application
    public long ApplicationId { get; set; }
    public long ApplicationStatusId { get; set; }
    public string? ApplicationStatusName { get; set; }
    public DateTime? ActionDate { get; set; }

    // Program
    public long ProgramId { get; set; }
    public string? ProgramName { get; set; }
    public string? ProgramCode { get; set; }

    // Faculty
    public long FacultyId { get; set; }
    public string? FacultyName { get; set; }

    // University
    public long UniversityId { get; set; }
    public string? UniversityName { get; set; }


    // UniversityCountry
    public long? UniversityCountryId { get; set; }
    public string? UniversityCountryName { get; set; }


        // Recently Added

        // Personal Information - Additional
        public string? Tribe { get; set; }

        // Address
        public string? House { get; set; }

        // Student Source
        public bool? FromDaSchool { get; set; }
        public string? DaStudentCode { get; set; }

        // Behavioral & Social Evaluation
        public long? FinancialNeedStatusId { get; set; }
        public string? FinancialNeedStatusName { get; set; }

        public long? SelfRelianceLevelId { get; set; }
        public string? SelfRelianceLevelName { get; set; }

        public long? MotivationLevelId { get; set; }
        public string? MotivationLevelName { get; set; }

        public long? FutureGoalsLevelId { get; set; }
        public string? FutureGoalsLevelName { get; set; }

        // Transfer Student Information
        public string? TransferInstitution { get; set; }
        public string? TransferProgram { get; set; }
        public string? TransferInstitutionType { get; set; }
        public decimal? TransferCredits { get; set; }
        public DateTime? TransferLastSemEnd { get; set; }
        public decimal? TransferGpa { get; set; }

        // Recommendation
        public string? RecommendationLetterPath { get; set; }
        public string? RecommendationLetterNotes { get; set; }

}
