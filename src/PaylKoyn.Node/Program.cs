using Chrysalis.Tx.Models;
using Chrysalis.Tx.Providers;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ICardanoDataProvider>(provider =>
    new Blockfrost(builder.Configuration.GetValue("BlockfrostApiKey", "previewBVVptlCv4DAR04h3XADZnrUdNTiJyHaJ")));

builder.Services.AddDbContextFactory<WalletDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<WalletService>();

WebApplication app = builder.Build();

IDbContextFactory<WalletDbContext> dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<WalletDbContext>>();
using WalletDbContext dbContext = dbContextFactory.CreateDbContext();
dbContext.Database.Migrate();

app.Run();
