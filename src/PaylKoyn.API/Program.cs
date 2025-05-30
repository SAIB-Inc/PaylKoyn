using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddFastEndpoints();
builder.Services.AddDbContextFactory<PaylKoynDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("CardanoContext"),
        x => x.MigrationsHistoryTable(
            "__EFMigrationsHistory",
            builder.Configuration.GetConnectionString("CardanoContextSchema")
        )
    );
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
    app.MapOpenApi();
    app.UseFastEndpoints();
}

builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddFastEndpoints(o => o.IncludeAbstractValidators = true);
builder.Services.AddDbContextFactory<PaylKoynDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("CardanoContext"),
        x => x.MigrationsHistoryTable(
            "__EFMigrationsHistory",
            builder.Configuration.GetConnectionString("CardanoContextSchema")
        )
    );
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