using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;

namespace DotnetDataProtectionExample;

internal static class DataProtection
{
    internal static BlobClient KeysFactory(IServiceProvider services)
    {
        var settings = services.GetRequiredService<IOptions<DataProtectionSettings>>().Value;
        var blobServiceClient = services.GetRequiredService<BlobServiceClient>();
        var containerClient = blobServiceClient.GetBlobContainerClient(settings.BlobContainer);
        return containerClient.GetBlobClient(settings.BlobName);
    }
}
