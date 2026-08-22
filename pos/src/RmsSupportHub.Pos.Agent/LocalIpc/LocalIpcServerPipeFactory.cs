using System.IO.Pipes;
using System.Security.AccessControl;

namespace RmsSupportHub.Pos.Agent.LocalIpc;

/// <summary>Small seam around server-instance creation so recovery can be tested without abstracting transport I/O.</summary>
public interface ILocalIpcServerPipeFactory
{
    NamedPipeServerStream Create(
        string pipeName,
        PipeDirection direction,
        int maxNumberOfServerInstances,
        PipeTransmissionMode transmissionMode,
        PipeOptions options,
        int inBufferSize,
        int outBufferSize,
        PipeSecurity security);
}

public sealed class WindowsLocalIpcServerPipeFactory : ILocalIpcServerPipeFactory
{
    public NamedPipeServerStream Create(
        string pipeName,
        PipeDirection direction,
        int maxNumberOfServerInstances,
        PipeTransmissionMode transmissionMode,
        PipeOptions options,
        int inBufferSize,
        int outBufferSize,
        PipeSecurity security) =>
        NamedPipeServerStreamAcl.Create(
            pipeName,
            direction,
            maxNumberOfServerInstances,
            transmissionMode,
            options,
            inBufferSize,
            outBufferSize,
            security);
}
