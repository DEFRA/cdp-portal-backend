using Microsoft.AspNetCore.Mvc;
using Microsoft.Diagnostics.Runtime;

namespace Defra.Cdp.Backend.Api.Endpoints;
using System.Diagnostics;

public static class DebugEndpoint
{
    public static void MapDebugEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/debug/memory", Memory);
        app.MapGet("/debug/threads", Threads);
        app.MapGet("/debug/log", Log);
    }


    // GET /debug/memory
    private static IResult Memory()
    {
        var process = Process.GetCurrentProcess();

        var memoryInfo = new
        {
            WorkingSetBytes = process.WorkingSet64,
            WorkingSetMB = process.WorkingSet64 / (1024 * 1024),
            PrivateMemoryBytes = process.PrivateMemorySize64,
            PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024),
            GCHeapBytes = GC.GetTotalMemory(forceFullCollection: false),
            GCHeapMB = GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024),
            VirtualMemoryBytes = process.VirtualMemorySize64,
            VirtualMemoryMB = process.VirtualMemorySize64 / (1024 * 1024)
        };

        return Results.Ok(memoryInfo);
    }


    // GET /debug/threads
    private static IResult Threads()
    {
        using var dataTarget = DataTarget.AttachToProcess(Process.GetCurrentProcess().Id, suspend: false);
        var runtime = dataTarget.ClrVersions[0].CreateRuntime();

        var threads = runtime.Threads
            .Where(t => t.IsAlive).Select(t => new
            {
                t.ManagedThreadId,
                t.OSThreadId,
                t.IsFinalizer,
                t.IsAlive,
                t.State,
                StackTrace = t.EnumerateStackTrace(false, 5).Select(f => $"{f.Method?.Signature}").ToList()

            }).ToList();

        return Results.Ok(threads);
    }

    // GET /debug/log?size=16
    private static IResult Log(ILoggerFactory loggerFactory, [FromQuery] int? size )
    {
        var logger = loggerFactory.CreateLogger("debug.logging");
        
        logger.LogInformation("/debug/log: {BigString}", new string('A', (size ?? 1) * 1024));
        return Results.Ok($"Logged ${size}k bytes");
    }
}