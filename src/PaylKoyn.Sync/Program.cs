using Argus.Sync.Data.Models;
using Argus.Sync.Extensions;
using PaylKoyn.Data.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCardanoIndexer<PaylKoynDbContext>(builder.Configuration);
builder.Services.AddReducers<PaylKoynDbContext, IReducerModel>(builder.Configuration);

WebApplication app = builder.Build();

app.Run();