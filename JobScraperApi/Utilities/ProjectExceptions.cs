namespace EazeTechnical.Utilities;

public class InvalidFactoryArgumentsException : Exception
{
    public InvalidFactoryArgumentsException() : base() { }

    public InvalidFactoryArgumentsException(string message) : base(message) { }

    public InvalidFactoryArgumentsException(string message, Exception innerException) : base(message, innerException) { }
}
