using System.Runtime;
using Argus.Sync.Data.Models;
using Argus.Sync.Extensions;
using PaylKoyn.Data.Models;
using PaylKoyn.Sync.Workers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Configure ThreadPool to prevent exhaustion
ThreadPool.SetMinThreads(Environment.ProcessorCount * 2, Environment.ProcessorCount * 2);
ThreadPool.SetMaxThreads(Environment.ProcessorCount * 4, Environment.ProcessorCount * 4);

// Configure GC for better memory management
GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

builder.Services.AddCardanoIndexer<PaylKoynDbContext>(builder.Configuration);
builder.Services.AddReducers<PaylKoynDbContext, IReducerModel>(builder.Configuration);
builder.Services.AddHostedService<TransactionSubmissionWorker>();

WebApplication app = builder.Build();


app.Run();