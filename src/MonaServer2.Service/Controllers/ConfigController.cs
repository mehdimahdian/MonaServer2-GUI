using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MonaServer2.Core.Config;

namespace MonaServer2.Service.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly MonaServerSettings _settings;

    public ConfigController(IOptions<MonaServerSettings> settings) =>
        _settings = settings.Value;

    [HttpGet]
    [ProducesResponseType<MonaServerConfig>(200)]
    [ProducesResponseType(404)]
    public ActionResult<MonaServerConfig> Get()
    {
        var path = ResolveConfigPath();
        if (!System.IO.File.Exists(path))
            return NotFound(new { error = $"Config not found: {path}" });

        return IniParser.Parse(path);
    }

    [HttpGet("raw")]
    [ProducesResponseType<string>(200)]
    [ProducesResponseType(404)]
    public ActionResult GetRaw()
    {
        var path = ResolveConfigPath();
        if (!System.IO.File.Exists(path))
            return NotFound(new { error = $"Config not found: {path}" });

        return Content(System.IO.File.ReadAllText(path), "text/plain");
    }

    [HttpPut]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public ActionResult Put([FromBody] MonaServerConfig config)
    {
        var path = ResolveConfigPath();
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest(new { error = "Config path not configured" });

        IniParser.Write(path, config);
        return NoContent();
    }

    [HttpPut("raw")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    public ActionResult PutRaw([FromBody] string rawIni)
    {
        var path = ResolveConfigPath();
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest(new { error = "Config path not configured" });

        System.IO.File.WriteAllText(path, rawIni, System.Text.Encoding.UTF8);
        return NoContent();
    }

    private string ResolveConfigPath()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ConfigPath))
            return _settings.ConfigPath;

        var exe = _settings.ResolvedExecutablePath;
        var dir = Path.GetDirectoryName(Path.GetFullPath(exe)) ?? AppContext.BaseDirectory;
        return Path.Combine(dir, "MonaServer.ini");
    }
}
