namespace ElectricalStoreAI.Application.Common;

public sealed class ApplicationException : Exception
{
    public ApplicationException(string message) : base(message)
    {
    }
}
