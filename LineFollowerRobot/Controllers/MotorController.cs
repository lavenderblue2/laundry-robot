using LineFollowerRobot.Services;
using Microsoft.AspNetCore.Mvc;

namespace LineFollowerRobot.Controllers;

[ApiController]
[Route("[controller]")]
public class MotorController : ControllerBase
{
    private readonly ILogger<MotorController> _logger;
    private readonly LineFollowerMotorService _motorService;

    public MotorController(
        ILogger<MotorController> logger, 
        LineFollowerMotorService motorService)
    {
        _logger = logger;
        _motorService = motorService;
    }

    [HttpPost("turn-around")]
    public async Task<IActionResult> TurnAround()
    {
        try
        {
            _logger.LogInformation("🔄 Turn around command received via API");

            await _motorService.TurnAroundAsync();

            return Ok(new {
                success = true,
                message = "180-degree turn completed successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error executing turn around command");
            return BadRequest(new {
                success = false,
                message = $"Turn around failed: {ex.Message}"
            });
        }
    }

    [HttpPost("forward")]
    public IActionResult MoveForward()
    {
        try
        {
            _logger.LogInformation("⬆️ Move forward command received via API");
            _motorService.MoveForward();

            return Ok(new {
                success = true,
                message = "Moving forward"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error executing move forward command");
            return BadRequest(new {
                success = false,
                message = $"Move forward failed: {ex.Message}"
            });
        }
    }

    [HttpPost("backward")]
    public IActionResult MoveBackward()
    {
        try
        {
            _logger.LogInformation("⬇️ Move backward command received via API");
            _motorService.MoveBackward();

            return Ok(new {
                success = true,
                message = "Moving backward"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error executing move backward command");
            return BadRequest(new {
                success = false,
                message = $"Move backward failed: {ex.Message}"
            });
        }
    }

    [HttpPost("turn-left")]
    public IActionResult TurnLeft()
    {
        try
        {
            _logger.LogInformation("⬅️ Turn left command received via API");
            _motorService.TurnLeft();

            return Ok(new {
                success = true,
                message = "Turning left"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error executing turn left command");
            return BadRequest(new {
                success = false,
                message = $"Turn left failed: {ex.Message}"
            });
        }
    }

    [HttpPost("turn-right")]
    public IActionResult TurnRight()
    {
        try
        {
            _logger.LogInformation("➡️ Turn right command received via API");
            _motorService.TurnRight();

            return Ok(new {
                success = true,
                message = "Turning right"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error executing turn right command");
            return BadRequest(new {
                success = false,
                message = $"Turn right failed: {ex.Message}"
            });
        }
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        try
        {
            _logger.LogInformation("🛑 Stop command received via API");
            _motorService.Stop();

            return Ok(new {
                success = true,
                message = "Motors stopped"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error executing stop command");
            return BadRequest(new {
                success = false,
                message = $"Stop failed: {ex.Message}"
            });
        }
    }
}