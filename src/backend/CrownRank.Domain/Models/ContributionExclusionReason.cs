using System;
using System.Collections.Generic;
using System.Text;

namespace CrownRank.Domain.Models
{
    public enum ContributionExclusionReason
    {
        PaymentReversed = 1,
        PaymentDisputed = 2,
        FraudulentPayment = 3,
        DuplicatePayment = 4,
        CrownRankError = 5,
        Other = 6
    }
}
