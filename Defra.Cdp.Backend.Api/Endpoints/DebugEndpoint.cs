using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Diagnostics.Runtime;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class DebugEndpoint
{
    public static void MapDebugEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/debug/memory", Memory);
        app.MapGet("/debug/threads", Threads);
        app.MapGet("/debug/log", Log);
    }


    // GET /debug/memory
    private static Ok<MemoryInfoResponse> Memory()
    {
        var process = Process.GetCurrentProcess();

        var memoryInfo = new MemoryInfoResponse(
            process.WorkingSet64,
            process.WorkingSet64 / (1024 * 1024),
            process.PrivateMemorySize64,
            process.PrivateMemorySize64 / (1024 * 1024),
            GC.GetTotalMemory(forceFullCollection: false),
            GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024),
            process.VirtualMemorySize64,
            process.VirtualMemorySize64 / (1024 * 1024));

        return TypedResults.Ok(memoryInfo);
    }


    // GET /debug/threads
    private static Ok<List<ThreadInfoResponse>> Threads()
    {
        using var dataTarget = DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, suspend: false);
        var runtime = dataTarget.ClrVersions[0].CreateRuntime();

        var threads = runtime.Threads
            .Where(t => t.IsAlive)
            .Select(t => new ThreadInfoResponse(
                t.ManagedThreadId,
                t.OSThreadId,
                t.IsFinalizer,
                t.IsAlive,
                t.State.ToString(),
                t.EnumerateStackTrace(false, 5).Select(f => $"{f.Method?.Signature}").ToList()))
            .ToList();

        return TypedResults.Ok(threads);
    }

    // GET /debug/log?size=16
    private static Ok<string> Log(ILoggerFactory loggerFactory, [FromQuery] int? size )
    {
        var logger = loggerFactory.CreateLogger("debug.logging");
        
        logger.LogInformation("/debug/log: {BigString}", new string('A', (size ?? 1) * 1024));
        return TypedResults.Ok($"Logged ${size}k bytes");
    }

    private sealed record MemoryInfoResponse(
        long WorkingSetBytes,
        long WorkingSetMB,
        long PrivateMemoryBytes,
        long PrivateMemoryMB,
        long GCHeapBytes,
        long GCHeapMB,
        long VirtualMemoryBytes,
        long VirtualMemoryMB);

    private sealed record ThreadInfoResponse(
        int ManagedThreadId,
        uint OSThreadId,
        bool IsFinalizer,
        bool IsAlive,
        string State,
        List<string> StackTrace);
}
