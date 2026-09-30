namespace Estuscia.Domain.Enums;

public enum PayrollCycleStatus
{
    Draft = 1,
    SubmittedByHR = 2,
    ApprovedByCompanyAdmin = 3,
    Processing = 4,
    Paid = 5,
    Rejected = 6,
    Locked = 7
}