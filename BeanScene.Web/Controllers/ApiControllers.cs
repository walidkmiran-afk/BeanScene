using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BeanScene.Web.Controllers;

public record ReservationDto(int Id, string Guest, string Email, string Phone, int SittingId, string Sitting,
    string StartTime, int DurationMinutes, int Guests, string Source, string Status, string? Notes,
    string[] Tables, Guid RowVersion);

public record LoginRequest(string Email, string Password);

/// <summary>
/// REST API that demonstrates the HTTP verbs GET, POST, PUT and DELETE (try it in Postman).
/// Log in first with POST /api/auth/login so the identity cookie is stored.
/// </summary>
[ApiController]
[Route("api/reservations")]
[Authorize(Roles = RoleNames.StaffOrManager)]
[IgnoreAntiforgeryToken]
public class ReservationsApiController : ControllerBase
{
    private readonly IReservationService _reservations;

    public ReservationsApiController(IReservationService reservations) => _reservations = reservations;

    // GET api/reservations?name=smith&status=Pending
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReservationDto>>> Get([FromQuery] ReservationSearchCriteria criteria)
    {
        var list = await _reservations.SearchAsync(criteria);
        return Ok(list.Select(ToDto));
    }

    // GET api/reservations/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservationDto>> GetById(int id)
    {
        var r = await _reservations.GetAsync(id);
        return r == null ? NotFound() : Ok(ToDto(r));
    }

    // POST api/reservations
    [HttpPost]
    public async Task<ActionResult<ReservationDto>> Create([FromBody] ReservationInput input)
    {
        var result = await _reservations.CreateAsync(input);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors });

        var created = await _reservations.GetAsync(result.Value!.ReservationId);
        return CreatedAtAction(nameof(GetById), new { id = created!.ReservationId }, ToDto(created));
    }

    // PUT api/reservations/5   (replace the editable fields; send the RowVersion you received)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ReservationInput input)
    {
        input.ReservationId = id;
        var result = await _reservations.UpdateAsync(input);
        if (result.Succeeded) return NoContent();
        return result.Errors.Any(e => e.Contains("not found")) ? NotFound() : Conflict(new { errors = result.Errors });
    }

    // PUT api/reservations/5/status?status=Confirmed
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromQuery] ReservationStatus status)
    {
        var result = await _reservations.ChangeStatusAsync(id, status);
        if (result.Succeeded) return NoContent();
        return result.Errors.Any(e => e.Contains("not found")) ? NotFound() : Conflict(new { errors = result.Errors });
    }

    // DELETE api/reservations/5   (Manager only)
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Manager)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _reservations.DeleteAsync(id);
        if (result.Succeeded) return NoContent();
        return result.Errors.Any(e => e.Contains("not found")) ? NotFound() : Conflict(new { errors = result.Errors });
    }

    private static ReservationDto ToDto(Reservation r) => new(
        r.ReservationId, r.Customer.FullName, r.Customer.Email, r.Customer.PhoneNumber, r.SittingId,
        r.Sitting.DisplayName, r.StartTime.ToString("HH:mm:ss"), r.DurationMinutes, r.NumberOfGuests,
        r.Source.ToString(), r.Status.ToString(), r.SpecialRequests,
        r.ReservationTables.Select(rt => rt.Table.TableName).OrderBy(n => n).ToArray(), r.RowVersion);
}

/// <summary>Cookie login for API clients such as Postman.</summary>
[ApiController]
[Route("api/auth")]
[IgnoreAntiforgeryToken]
public class AuthApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signIn;

    public AuthApiController(SignInManager<ApplicationUser> signIn) => _signIn = signIn;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _signIn.PasswordSignInAsync(request.Email, request.Password, false, lockoutOnFailure: true);
        return result.Succeeded ? Ok(new { message = "Logged in" }) : Unauthorized(new { message = "Invalid email or password" });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return NoContent();
    }
}
