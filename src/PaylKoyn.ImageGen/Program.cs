using Chrysalis.Tx.Models;
using Chrysalis.Tx.Providers;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.ImageGen.Services;
using PaylKoyn.ImageGen.Workers;
using PaylKoyn.Data.Extensions;
using PaylKoyn.Data.Services;
using PaylKoyn.ImageGen.Data;
using PaylKoyn.ImageGen.Services;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddFastEndpoints(o => o.IncludeAbstractValidators = true);
builder.Services.AddCardanoProvider(builder.Configuration);

builder.Services.AddSingleton<NftRandomizerService>();
builder.Services.AddSingleton<TransactionService>();
builder.Services.AddSingleton<MintingService>();
builder.Services.AddSingleton<WalletService>();
builder.Services.AddSingleton<TransactionTemplateService>();

builder.Services.AddDbContextFactory<MintDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient("PaylKoynNodeClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetValue("PaylKoynNodeUrl", "http://localhost:5246/api/v1"));
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Workers
builder.Services.AddHostedService<MintWorker>();
builder.Services.AddHostedService<MintPaymentWorker>();
builder.Services.AddHostedService<FileUploadPaymentWorker>();
builder.Services.AddHostedService<FileUploadWorker>();

WebApplication app = builder.Build();

// ensure migrations are applied
using (IServiceScope scope = app.Services.CreateScope())
{
    IDbContextFactory<MintDbContext> dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<MintDbContext>>();
    using MintDbContext dbContext = dbContextFactory.CreateDbContext();
    await dbContext.Database.MigrateAsync();
}

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