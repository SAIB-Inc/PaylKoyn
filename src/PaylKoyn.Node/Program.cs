using System.Text;
using Chrysalis.Tx.Models;
using Chrysalis.Tx.Providers;
using Chrysalis.Wallet.Models.Keys;
using Chrysalis.Wallet.Words;
using Microsoft.EntityFrameworkCore;
using PaylKoyn.Data.Models;
using PaylKoyn.Data.Services;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ICardanoDataProvider>(provider =>
    new Blockfrost(builder.Configuration.GetValue("BlockfrostApiKey", "previewBVVptlCv4DAR04h3XADZnrUdNTiJyHaJ")));

builder.Services.AddDbContextFactory<WalletDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<WalletService>();
builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<TransactionService>();

WebApplication app = builder.Build();

IDbContextFactory<WalletDbContext> dbContextFactory = app.Services.GetRequiredService<IDbContextFactory<WalletDbContext>>();
using WalletDbContext dbContext = dbContextFactory.CreateDbContext();
dbContext.Database.Migrate();

var seed = Mnemonic.Generate(English.Words, 24);
Console.WriteLine(string.Join(" ", seed.Words));
var fileService = app.Services.GetRequiredService<FileService>();
var wallet = await fileService.RequestUploadAsync();
var fileContent = Encoding.ASCII.GetBytes(string.Join(",", Enumerable.Range(0, 5000).Select(i => "Hello, World!")));
var fileContentSize = fileContent.Length;
Console.WriteLine($"File content size: {fileContentSize} bytes");
var contentType = "text/plain";
var fileName = "testfile.txt";

try
{
    await fileService.UploadAsync(wallet.Address, fileContent, contentType, fileName);
}
catch (Exception ex)
{
    Console.WriteLine($"Error during file upload: {ex.Message}");
}

app.Run();
