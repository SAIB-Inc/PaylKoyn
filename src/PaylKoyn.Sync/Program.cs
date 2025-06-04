using Argus.Sync.Data.Models;
using Argus.Sync.Extensions;
using PaylKoyn.Data.Models;
using PaylKoyn.Sync.Workers;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCardanoIndexer<PaylKoynDbContext>(builder.Configuration);
builder.Services.AddReducers<PaylKoynDbContext, IReducerModel>(builder.Configuration);
builder.Services.AddHostedService<TransactionSubmissionWorker>();

WebApplication app = builder.Build();


app.Run();