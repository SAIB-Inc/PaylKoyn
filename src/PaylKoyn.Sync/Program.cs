using Argus.Sync.Data.Models;
using Argus.Sync.Extensions;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddCardanoIndexer<PaylKoynDbContext>(builder.Configuration);
builder.Services.AddReducers<PaylKoynDbContext, IReducerModel>(builder.Configuration);

WebApplication app = builder.Build();

// Run database migrations at startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PaylKoynDbContext>();
    try
    {
        app.Logger.LogInformation("Running database migrations...");
        context.Database.Migrate();
        app.Logger.LogInformation("Database migrations completed successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Error running database migrations: {Message}", ex.Message);
        throw;
    }
}

app.Run();