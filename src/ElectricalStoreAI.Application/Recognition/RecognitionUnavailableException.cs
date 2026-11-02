namespace ElectricalStoreAI.Application.Recognition;

/// <summary>
/// The local recognition model is missing or could not be loaded. The message is safe to show to users
/// (it never contains absolute file paths or inner exception details).
/// </summary>
public sealed class RecognitionUnavailableException : Exception
{
    public RecognitionUnavailableException(string message) : base(message)
    {
    }
}
