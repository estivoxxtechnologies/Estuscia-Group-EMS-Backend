namespace Estuscia.Domain.Enums;

public enum UserRole
{
    SuperAdmin,
    CompanyAdmin,
    HrOps,
    BranchManager,
    SalesStaff,
    Developer,
    SupportStaff,
    KnowledgeTrainer
}

public enum WorkLogType
{
    Sales,
    Developer,
    General
}

public enum AttendanceStatus
{
    Present,
    Late,
    HalfDay,
    Absent,
    OnLeave
}

public enum DealStatus
{
    PendingReview,
    Verified,
    PayoutProcessed,
    Rejected
}
