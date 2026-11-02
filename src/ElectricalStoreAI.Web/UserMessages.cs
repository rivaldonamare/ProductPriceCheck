using ElectricalStoreAI.Application.Recognition;
using ElectricalStoreAI.Domain.Common;
using AppException = ElectricalStoreAI.Application.Common.ApplicationException;

namespace ElectricalStoreAI.Web;

/// <summary>
/// Maps exceptions to messages that are safe to show: only our own validation/setup exceptions carry
/// user-facing text; anything else gets the generic fallback (and must be logged by the caller).
/// </summary>
public static class UserMessages
{
    public static bool IsUserFacing(Exception exception)
        => exception is DomainException or AppException or RecognitionUnavailableException;

    public static string From(Exception exception, string fallback)
        => IsUserFacing(exception) ? exception.Message : fallback;
}
