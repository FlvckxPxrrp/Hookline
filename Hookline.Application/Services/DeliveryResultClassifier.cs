using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hookline.Application.Services
{
    public class DeliveryResultClassifier
    {
        public static DeliveryOutcome Classify(
            (bool IsSuccess, int? StatusCode, string? Error) result)
        {
            if (result.IsSuccess)
                return DeliveryOutcome.Success;

            if (result.StatusCode is >= 500 and <= 599)
                return DeliveryOutcome.Retry;

            if (result.StatusCode is null && result.Error == "Timeout")
                return DeliveryOutcome.Retry;

            return DeliveryOutcome.PermanentFailure;
        }
    }
}
