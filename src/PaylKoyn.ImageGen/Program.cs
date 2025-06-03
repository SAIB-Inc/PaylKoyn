using Chrysalis.Tx.Models;
using Chrysalis.Tx.Providers;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Paylkoyn.ImageGen.Services;
using PaylKoyn.Data.Services;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddFastEndpoints(o => o.IncludeAbstractValidators = true);
builder.Services.AddSingleton<NftRandomizerService>();
builder.Services.AddSingleton<TransactionService>();
builder.Services.AddSingleton<MintingService>();
builder.Services.AddSingleton<WalletService>();
builder.Services.AddSingleton<TransactionTemplateService>();
builder.Services.AddSingleton<ICardanoDataProvider>(provider =>
    new Blockfrost(builder.Configuration.GetValue("BlockfrostApiKey", "previewBVVptlCv4DAR04h3XADZnrUdNTiJyHaJ")));
builder.Services.AddDbContextFactory<MintDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient("PaylKoynNodeClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue("PaylKoynNodeUrl", "http://localhost:5246/api/v1"));
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
    c.Versioning.Prefix = "v";
    c.Versioning.DefaultVersion = 1;
    c.Versioning.PrependToRoute = true;
});

app.Run();