using Microsoft.AspNetCore.Mvc;
using MonaServer2.Core.Api;
using MonaServer2.Core.Models;

namespace MonaServer2.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly MonaApiClient _api;

    public SessionsController(MonaApiClient api) => _api = api;

    [HttpGet]
    [ProducesResponseType<List<Session>>(200)]
    public Task<List<Session>> GetAll(CancellationToken ct) =>
        _api.GetSessionsAsync(ct);
}
