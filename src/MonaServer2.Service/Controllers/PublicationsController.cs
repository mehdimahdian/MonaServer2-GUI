using Microsoft.AspNetCore.Mvc;
using MonaServer2.Core.Api;
using MonaServer2.Core.Models;

namespace MonaServer2.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PublicationsController : ControllerBase
{
    private readonly MonaApiClient _api;

    public PublicationsController(MonaApiClient api) => _api = api;

    [HttpGet]
    [ProducesResponseType<List<Publication>>(200)]
    public Task<List<Publication>> GetAll(CancellationToken ct) =>
        _api.GetPublicationsAsync(ct);
}
