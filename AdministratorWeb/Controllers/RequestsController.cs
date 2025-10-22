using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;
using AdministratorWeb.Models.DTOs;
using AdministratorWeb.Services;

namespace AdministratorWeb.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class RequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RequestsController> _logger;
        private readonly IRobotManagementService _robotService;

        public RequestsController(ApplicationDbContext context, ILogger<RequestsController> logger,
            IRobotManagementService robotService)
        {
            _context = context;
            _logger = logger;
            _robotService = robotService;
        }

        public async Task<IActionResult> Index()
        {
            var requests = await _context.LaundryRequests
                .Include(r => r.HandledBy)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            var robots = await _robotService.GetAllRobotsAsync();
            var availableRobots = robots.Where(r => r.IsActive && !r.IsOffline).ToList();

            var dto = new RequestsIndexDto
            {
                Requests = requests,
                AvailableRobots = availableRobots
            };

            return View(dto);
        }

        [HttpPost]
        public async Task<IActionResult> AcceptRequest(int requestId)
        {
            var request = await _context.LaundryRequests.FindAsync(requestId);

            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

            // Get the user to access their assigned beacon
            var user = request != null ? await _context.Users.FindAsync(request.CustomerId) : null;

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }


            try
            {
                // Auto-assign robot using smart assignment logic
                var assignedRobot = await AutoAssignRobotAsync(requestId);
                if (assignedRobot == null)
                {
                    TempData["Error"] = "No robots available for assignment.";
                    return RedirectToAction(nameof(Index));
                }

                // Update request
                request.Status = RequestStatus.Accepted;
                request.AssignedRobotName = assignedRobot.Name;
                request.HandledById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                request.ProcessedAt = DateTime.UtcNow;
                request.AcceptedAt = DateTime.UtcNow;

                // get user's room
                var userRooms = await _context.BluetoothBeacons.Where(x => x.RoomName == user.RoomName)
                    .ToListAsync();


                _logger.LogInformation(
                    "Request {RequestId} beacon assignment: User {CustomerId} has beacon {UserBeacon}, assigned to request: {RequestBeacon}",
                    requestId, request.CustomerId, user?.AssignedBeaconMacAddress ?? "None",
                    request.AssignedBeaconMacAddress ?? "None");
                

                // Update robot status
                assignedRobot.Status = RobotStatus.Busy;
                assignedRobot.CurrentTask = $"Handling request #{requestId}";

                await _context.SaveChangesAsync();

                // **CRITICAL: Start robot line following to target beacon**
                var lineFollowingStarted = await _robotService.SetLineFollowingAsync(assignedRobot.Name, true);

                if (!lineFollowingStarted)
                {
                    _logger.LogWarning("Failed to start line following for robot {RobotName}", assignedRobot.Name);
                }

                _logger.LogInformation(
                    "Request {RequestId} accepted, robot {RobotName} dispatched to beacon {BeaconMac}",
                    requestId, assignedRobot.Name, request.AssignedBeaconMacAddress);

                TempData["Success"] =
                    $"Request #{requestId} accepted, robot {assignedRobot.Name} dispatched to {user?.RoomName}. Cost: ${request.TotalCost:F2}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting request {RequestId}", requestId);
                TempData["Error"] = "An error occurred while processing the request.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeclineRequest(int requestId, string reason)
        {
            var request = await _context.LaundryRequests.FindAsync(requestId);
            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                request.Status = RequestStatus.Declined;
                request.DeclineReason = reason ?? "No reason provided";
                request.HandledById = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                request.ProcessedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Request {RequestId} declined by user {UserId} with reason: {Reason}",
                    requestId, User.Identity?.Name, reason);

                TempData["Success"] = $"Request #{requestId} declined.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error declining request {RequestId}", requestId);
                TempData["Error"] = "An error occurred while processing the request.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CompleteRequest(int requestId)
        {
            var request = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                request.Status = RequestStatus.Completed;
                request.CompletedAt = DateTime.UtcNow;

                // Free up the robot
                if (!string.IsNullOrEmpty(request.AssignedRobotName))
                {
                    var robot = await _robotService.GetRobotAsync(request.AssignedRobotName);
                    if (robot != null)
                    {
                        robot.Status = RobotStatus.Available;
                        robot.CurrentTask = null;
                    }
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Request {RequestId} completed by user {UserId}",
                    requestId, User.Identity?.Name);

                TempData["Success"] = $"Request #{requestId} marked as completed.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing request {RequestId}", requestId);
                TempData["Error"] = "An error occurred while processing the request.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> MarkForPickupDelivery(int requestId)
        {
            var request = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }

            if (request.Status != RequestStatus.Washing)
            {
                TempData["Error"] = "Request must be in Washing status to mark for pickup/delivery.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                request.Status = RequestStatus.FinishedWashing;
                request.ProcessedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Request {RequestId} marked as finished washing and ready for pickup/delivery by user {UserId}",
                    requestId, User.Identity?.Name);

                TempData["Success"] = $"Request #{requestId} is now ready for customer pickup or delivery.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking request {RequestId} for pickup/delivery", requestId);
                TempData["Error"] = "An error occurred while processing the request.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> StartDelivery(int requestId)
        {
            var request = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction(nameof(Index));
            }

            if (request.Status != RequestStatus.FinishedWashingReadyToDeliver)
            {
                TempData["Error"] = "Request must be in Ready to Deliver status to start delivery.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Change status to GoingToRoom so robot starts moving
                request.Status = RequestStatus.FinishedWashingGoingToRoom;
                request.ProcessedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Request {RequestId} delivery started - robot dispatched to customer room by user {UserId}",
                    requestId, User.Identity?.Name);

                TempData["Success"] = $"Delivery started for request #{requestId}. Robot is on its way to customer room.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting delivery for request {RequestId}", requestId);
                TempData["Error"] = "An error occurred while starting the delivery.";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var request = await _context.LaundryRequests
                .Include(r => r.HandledBy)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            return View(request);
        }

        /// <summary>
        /// Auto-assign a robot to a request using smart assignment logic
        /// Priority: 1) Available robots 2) Least recently assigned busy robot
        /// </summary>
        private async Task<ConnectedRobot?> AutoAssignRobotAsync(int requestId)
        {
            try
            {
                // Get all active online robots
                var allRobots = await _robotService.GetAllRobotsAsync();
                var activeRobots = allRobots.Where(r => r.IsActive && !r.IsOffline).ToList();

                if (!activeRobots.Any())
                {
                    _logger.LogWarning("No active robots available for auto-assignment");
                    return null;
                }

                // First priority: Find available (non-busy) robots
                var availableRobots = activeRobots.Where(r => r.Status == RobotStatus.Available).ToList();

                if (availableRobots.Any())
                {
                    // Return the first available robot (could be enhanced with proximity logic later)
                    var selectedRobot = availableRobots.First();
                    _logger.LogInformation("Auto-assigned available robot {RobotName} to request {RequestId}",
                        selectedRobot.Name, requestId);
                    return selectedRobot;
                }

                // Second priority: All robots are busy, find the least recently assigned one
                var busyRobots = activeRobots.Where(r => r.Status == RobotStatus.Busy).ToList();

                if (busyRobots.Any())
                {
                    // Find the robot with the oldest current task assignment
                    var leastRecentlyAssigned = busyRobots
                        .OrderBy(r => r.LastPing) // Using LastPing as proxy for assignment time
                        .First();

                    // Find and reassign the current request of this robot
                    var currentRequest = await _context.LaundryRequests
                        .Where(req => req.AssignedRobotName == leastRecentlyAssigned.Name &&
                                      req.Status != RequestStatus.Completed &&
                                      req.Status != RequestStatus.Cancelled &&
                                      req.Status != RequestStatus.Declined)
                        .OrderByDescending(req => req.AcceptedAt)
                        .FirstOrDefaultAsync();

                    if (currentRequest != null)
                    {
                        // Reset the current request back to pending for reassignment
                        currentRequest.AssignedRobotName = null;
                        currentRequest.Status = RequestStatus.Pending;
                        currentRequest.AcceptedAt = null;

                        _logger.LogInformation(
                            "Reassigning robot {RobotName} from request {OldRequestId} to request {NewRequestId}",
                            leastRecentlyAssigned.Name, currentRequest.Id, requestId);
                    }

                    // Reset robot status to be assigned to new request
                    leastRecentlyAssigned.Status = RobotStatus.Available;
                    leastRecentlyAssigned.CurrentTask = null;

                    return leastRecentlyAssigned;
                }

                _logger.LogWarning("No suitable robots found for auto-assignment");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto-assignment for request {RequestId}", requestId);
                return null;
            }
        }

        /// <summary>
        /// API endpoint to get detailed request information for accept modal
        /// </summary>
        [HttpGet("api/requests/{requestId}/details")]
        public async Task<IActionResult> GetRequestDetails(int requestId)
        {
            try
            {
                var request = await _context.LaundryRequests
                    .FirstOrDefaultAsync(r => r.Id == requestId);

                if (request == null)
                {
                    return NotFound(new { error = "Request not found" });
                }

                // Get assigned robot details if any
                string? robotName = null;
                double? detectedWeight = null;
                if (!string.IsNullOrEmpty(request.AssignedRobotName))
                {
                    robotName = request.AssignedRobotName;
                    var robot = await _robotService.GetRobotAsync(request.AssignedRobotName);
                    detectedWeight = robot?.WeightKg;
                }

                // Get room name from customer record
                string? roomName = null;
                var customer = await _context.Users.FindAsync(request.CustomerId);
                roomName = customer?.RoomName;

                var response = new
                {
                    requestId = request.Id,
                    customerName = request.CustomerName,
                    customerId = request.CustomerId,
                    address = request.Address,
                    requestedAt = request.RequestedAt,
                    status = request.Status.ToString(),
                    weight = request.Weight,
                    assignedRobotName = robotName,
                    roomName = roomName,
                    detectedWeightKg = detectedWeight,
                    totalCost = request.TotalCost
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting request details for {RequestId}", requestId);
                return StatusCode(500, new { error = "Failed to get request details" });
            }
        }

        [HttpGet("/api/requests-data")]
        public async Task<IActionResult> GetRequestsData()
        {
            try
            {
                var requests = await _context.LaundryRequests
                    .OrderByDescending(r => r.RequestedAt)
                    .Select(r => new
                    {
                        id = r.Id,
                        customerName = r.CustomerName,
                        customerPhone = r.CustomerPhone,
                        address = r.Address,
                        instructions = r.Instructions,
                        type = r.Type.ToString(),
                        status = r.Status.ToString(),
                        weight = r.Weight,
                        totalCost = r.TotalCost,
                        requestedAt = r.RequestedAt,
                        scheduledAt = r.ScheduledAt,
                        assignedRobotName = r.AssignedRobotName,
                        declineReason = r.DeclineReason,
                        arrivedAtRoomAt = r.ArrivedAtRoomAt
                    })
                    .ToListAsync();

                return Ok(new { requests });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting requests data");
                return StatusCode(500, new { error = "Failed to get requests data" });
            }
        }
    }
}