using Estuscia.Domain.Common;
using Estuscia.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Estuscia.Domain.Entities
{
    public class TenantPayment : BaseEntity
    {
        public int TenantId { get; set; }

        public int TotalBranches { get; set; }

        public PaymentMode PaymentMode { get; set; }

        public decimal Amount { get; set; }

        public PaymentStatus PaymentStatus { get; set; }

        public DateTime? PaymentDateUtc { get; set; }

        public DateTime? ValidFromUtc { get; set; }

        public DateTime? ValidUntilUtc { get; set; }

        public bool RegistrationStatus { get; set; }

        public string? Notes { get; set; }

        public Tenant Tenant { get; set; } = null!;
    }
}
