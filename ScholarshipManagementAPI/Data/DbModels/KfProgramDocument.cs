using System;
using System.Collections.Generic;

namespace ScholarshipManagementAPI.Data.DbModels;

public partial class KfProgramDocument
{
    public long ProgramDocumentId { get; set; }

    public long ProgramId { get; set; }

    public long DocumentTypeId { get; set; }

    public bool IsRequired { get; set; }

    public int? DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime CreatedDate { get; set; }

    public long? UpdatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public virtual KfDocumentType DocumentType { get; set; } = null!;

    public virtual ICollection<KfStudentProgramDocument> KfStudentProgramDocuments { get; set; } = new List<KfStudentProgramDocument>();

    public virtual KfProgram Program { get; set; } = null!;
}
