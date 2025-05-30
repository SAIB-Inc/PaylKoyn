using System.Text;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Providers;
using FastEndpoints;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
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

builder.Services.AddOpenApi();
builder.Services.AddFastEndpoints(o => o.IncludeAbstractValidators = true);
builder.Services.AddSingleton<ICardanoDataProvider>(provider =>
    new Blockfrost(builder.Configuration.GetValue("BlockfrostApiKey", "previewBVVptlCv4DAR04h3XADZnrUdNTiJyHaJ")));

builder.Services.AddDbContextFactory<WalletDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<WalletService>();
builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<TransactionService>();


WebApplication app = builder.Build();

// IDbContextFactory<WalletDbContext> dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<WalletDbContext>>();
// using WalletDbContext dbContext = dbContextFactory.CreateDbContext();
// dbContext.Database.Migrate();

// var walletService = app.Services.GetRequiredService<WalletService>();
// var fileService = app.Services.GetRequiredService<FileService>();
// var wallet = await walletService.GenerateWalletAsync();
// var privateKey = walletService.GetPaymentPrivateKey(wallet.Index);
// var fileContent = Encoding.ASCII.GetBytes(string.Join(",", Enumerable.Range(0, 5000).Select(i => "Hello, World!")));
// var fileContentSize = fileContent.Length;
// Console.WriteLine($"File content size: {fileContentSize} bytes");
// var contentType = "text/plain";
// var fileName = "testfile.txt";

// try
// {
//     await fileService.UploadAsync(wallet.Address, fileContent, contentType, fileName, privateKey);
// }
// catch (Exception ex)
// {
//     Console.WriteLine($"Error during file upload: {ex.Message}");
// }

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
