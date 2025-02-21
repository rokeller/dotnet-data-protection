using Azure.Identity;
using DotnetDataProtectionExample;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAzureClients((options) =>
{
    options.UseCredential(new DefaultAzureCredential());
    options.AddBlobServiceClient(builder.Configuration.GetSection("BlobStorage"));
});

builder.Services
    .AddControllers().Services

    .Configure<DataProtectionSettings>(builder.Configuration.GetSection("DataProtection"))

    .AddDataProtection()
    .PersistKeysToAzureBlobStorage(DataProtection.KeysFactory)
    .ProtectKeysWithAzureKeyVault(
        builder.Configuration.GetValue<Uri>("DataProtection:KeyId"),
        new DefaultAzureCredential())
    ;

var app = builder.Build();

app.MapControllers();
app.Run();
