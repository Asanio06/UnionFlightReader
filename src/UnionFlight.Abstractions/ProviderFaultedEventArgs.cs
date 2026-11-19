namespace UnionFlight;

public sealed class ProviderFaultedEventArgs : EventArgs
{
    public ProviderFaultedEventArgs(Exception exception) => Exception = exception;

    public Exception Exception { get; }
}
