using System.Text.Json.Serialization;
using Wiaoj.Compensation; // Kendi taşıdığın namespace

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

if(app.Environment.IsDevelopment()) {
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ============================================================================
// ENDPOINT: Complex AI Document Processing Workflow
// ============================================================================
app.MapPost("/api/documents/process", async (
    ProcessDocumentRequest request,
    int failAtStep = 0,
    CancellationToken cancellationToken = default) => {
        var context = new DocumentProcessingContext {
            DocumentId = Guid.NewGuid(),
            UserId = request.UserId,
            FileName = request.FileName,
            CreditsRequired = 50
        };

        var pipeline = new CompensationPipeline<DocumentProcessingContext>();

        // ------------------------------------------------------------------------
        // STEP 1: Concurrency Lock & Rate Limiting
        // ------------------------------------------------------------------------
        pipeline.AddStep(
            name: "AcquireUserProcessingLock",
            execute: async (ctx, ct) => {
                ctx.LockKey = $"lock:user:{ctx.UserId}:doc_processing";
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[EXECUTE 1] Acquired distributed lock: '{ctx.LockKey}' (ActiveJobs: +1)");

                if(failAtStep == 1)
                    throw new InvalidOperationException("User already has an active processing job running.");

                await Task.CompletedTask;
            },
            compensate: async (ctx, rollbackCt) => {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[ROLLBACK 1] Released lock: '{ctx.LockKey}' (ActiveJobs: -1)");
                await Task.CompletedTask;
            }
        );

        // ------------------------------------------------------------------------
        // STEP 2: Balance & Credit Reservation
        // ------------------------------------------------------------------------
        pipeline.AddStep(
            name: "DeductUserCredits",
            execute: async (ctx, ct) => {
                ctx.CreditsDeducted = ctx.CreditsRequired;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[EXECUTE 2] Deducted {ctx.CreditsDeducted} credits from User '{ctx.UserId}'.");

                if(failAtStep == 2)
                    throw new InvalidOperationException("Insufficient account credits to perform AI analysis.");

                await Task.CompletedTask;
            },
            compensate: async (ctx, rollbackCt) => {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[ROLLBACK 2] Refunded {ctx.CreditsDeducted} credits back to User '{ctx.UserId}'.");
                  
                ctx.CreditsDeducted = 0;
                await Task.CompletedTask;
            }
        );

        // ------------------------------------------------------------------------
        // STEP 3: Local Disk Workspace & File Extraction
        // ------------------------------------------------------------------------
        pipeline.AddStep(
            name: "CreateDiskWorkspaceAndExtract",
            execute: async (ctx, ct) => {
                ctx.WorkspaceDirectoryPath = Path.Combine(Path.GetTempPath(), "workspaces", ctx.DocumentId.ToString());
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[EXECUTE 3] Created temporary workspace directory: '{ctx.WorkspaceDirectoryPath}' and extracted 12 pages.");

                if(failAtStep == 3)
                    throw new IOException("Disk quota exceeded or corrupted PDF stream.");

                await Task.CompletedTask;
            },
            compensate: async (ctx, rollbackCt) => {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[ROLLBACK 3] Cleaned up temporary workspace directory: '{ctx.WorkspaceDirectoryPath}'");
                ctx.WorkspaceDirectoryPath = null;
                await Task.CompletedTask;
            }
        );

        // ------------------------------------------------------------------------
        // STEP 4: External AI Cluster Dispatch (Compute Job)
        // ------------------------------------------------------------------------
        pipeline.AddStep(
            name: "DispatchToAiCluster",
            execute: async (ctx, ct) => {
                ctx.RemoteAiJobId = $"job_cluster_{Guid.NewGuid():N}";
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[EXECUTE 4] Dispatched document to AI Cloud Cluster. JobId: '{ctx.RemoteAiJobId}'");

                if(failAtStep == 4)
                    throw new HttpRequestException("AI Cluster returned 504 Gateway Timeout while processing embeddings.");

                await Task.CompletedTask;
            },
            compensate: async (ctx, rollbackCt) => {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[ROLLBACK 4] Sent cancellation signal to AI Cloud Cluster for JobId: '{ctx.RemoteAiJobId}'");
                await Task.CompletedTask;
            }
        );

        // ------------------------------------------------------------------------
        // STEP 5: Database Commit & Search Indexing
        // ------------------------------------------------------------------------
        pipeline.AddStep(
            name: "PersistDocumentAndIndex",
            execute: async (ctx, ct) => {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[EXECUTE 5] Successfully indexed embeddings into Vector DB for Document: '{ctx.DocumentId}'");

                if(failAtStep == 5)
                    throw new InvalidOperationException("Vector DB transaction conflict / unique constraint violated.");

                await Task.CompletedTask;
            },
            compensate: async (ctx, rollbackCt) => {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[ROLLBACK 5] Reverted document status to 'Failed' in database.");
                await Task.CompletedTask;
            }
        );

        // ------------------------------------------------------------------------
        // EXECUTION
        // ------------------------------------------------------------------------
        var report = await pipeline.RunAsync(
            context,
            rollbackTimeout: TimeSpan.FromSeconds(5),
            cancellationToken
        );

        Console.ResetColor();

        return report.IsSuccess
            ? Results.Ok(report)
            : Results.BadRequest(report);
    })
.WithName("ProcessDocument");

app.Run();

// ============================================================================
// DTO & CONTEXT MODELS
// ============================================================================
public sealed record ProcessDocumentRequest(Guid UserId, string FileName);

public sealed class DocumentProcessingContext {
    public Guid DocumentId { get; set; }
    public Guid UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int CreditsRequired { get; set; }

    // State tracked across steps
    public string? LockKey { get; set; }
    public int CreditsDeducted { get; set; }
    public string? WorkspaceDirectoryPath { get; set; }
    public string? RemoteAiJobId { get; set; }
}