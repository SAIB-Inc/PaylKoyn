using System.Text;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Providers;
using FastEndpoints;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Node.Workers;
using PaylKoyn.Data.Extensions;
using PaylKoyn.Data.Services;
using PaylKoyn.Node.Data;
using PaylKoyn.Node.Services;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromHours(1);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddHostedService<SubmitWorker>();
builder.Services.AddHostedService<PaymentWorker>();
builder.Services.AddHostedService<AirdropWorker>();
builder.Services.AddHostedService<TxWorker>();
builder.Services.AddHostedService<RefundWorker>();

builder.Services.AddOpenApi();
builder.Services.AddFastEndpoints(o => o.IncludeAbstractValidators = true);
builder.Services.AddCardanoProvider(builder.Configuration);

builder.Services.AddDbContextFactory<WalletDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<WalletService>();
builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<TransactionService>();
builder.Services.AddSingleton<FileCacheService>();
builder.Services.AddSingleton<AssetTransferService>();

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
    IDbContextFactory<WalletDbContext> dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WalletDbContext>>();
    using WalletDbContext dbContext = dbContextFactory.CreateDbContext();
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
