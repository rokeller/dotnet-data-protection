namespace DotnetDataProtectionExample;

internal sealed class DataProtectionSettings
{
    public string BlobContainer { get; set; } = "keys";
    public string BlobName { get; set; } = "keys.xml";
}
