using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace DotnetDataProtectionExample.Controllers;

[ApiController]
[Route("[controller]")]
public class DataProtectionDemoController(IDataProtectionProvider dataProtectionProvider) : ControllerBase
{
    private readonly IDataProtector dataProtector = dataProtectionProvider.CreateProtector("my-demo-secrets");

    [HttpPost("protect")]
    public ActionResult<string> Protect(
        [FromForm] string plaintext)
    {
        return Ok(dataProtector.Protect(plaintext));
    }

    [HttpPost("unprotect")]
    public ActionResult<string> Unprotect(
        [FromForm] string ciphertext)
    {
        return Ok(dataProtector.Unprotect(ciphertext));
    }
}
