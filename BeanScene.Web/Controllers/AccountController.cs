using BeanScene.Web.Services;
using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly ApplicationDbContext _db;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, ApplicationDbContext db)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");   // already logged in
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signIn.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return LocalRedirect(model.ReturnUrl);
            return RedirectToAction("Index", "Home");
        }

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "This account is locked. Try again later."
            : "Invalid email or password.");
        return View(model);
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");   // members and staff do not need to register
        return View(new RegisterViewModel());
    }

    /// <summary>Registers a guest as a Member: creates the login, adds the Member role and links a Customer record.</summary>
    [HttpPost, AllowAnonymous]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber
        };

        var create = await _users.CreateAsync(user, model.Password);
        if (!create.Succeeded)
        {
            foreach (var error in create.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        try
        {
            await _users.AddToRoleAsync(user, RoleNames.Member);

            // If the guest has booked before with the same email, link that Customer; otherwise create one.
            var email = model.Email.Trim().ToLower();
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == email && c.UserId == null);
            if (customer == null)
            {
                customer = new Customer { Email = model.Email.Trim() };
                _db.Customers.Add(customer);
            }
            customer.FirstName = model.FirstName.Trim();
            customer.LastName = model.LastName.Trim();
            customer.PhoneNumber = ReservationService.NormalizePhone(model.PhoneNumber);
            customer.UserId = user.Id;
            await _db.SaveChangesAsync();
        }
        catch (Exception)
        {
            await _users.DeleteAsync(user);   // do not leave a login without a customer record
            ModelState.AddModelError(string.Empty, "Registration failed. Please try again.");
            return View(model);
        }

        await _signIn.SignInAsync(user, isPersistent: false);
        TempData["Success"] = "Welcome to Bean Scene! Your member account has been created.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ---- Member account page ----------------------------------------------------

    [HttpGet, Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> Details()
    {
        var userId = _users.GetUserId(User);
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer == null) return NotFound();

        ViewBag.Email = customer.Email;
        return View(new AccountDetailsViewModel { FirstName = customer.FirstName, LastName = customer.LastName, PhoneNumber = customer.PhoneNumber });
    }

    [HttpPost, Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> Details(AccountDetailsViewModel model)
    {
        var userId = _users.GetUserId(User);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        if (customer == null) return NotFound();

        ViewBag.Email = customer.Email;
        if (!ModelState.IsValid) return View(model);

        customer.FirstName = model.FirstName.Trim();
        customer.LastName = model.LastName.Trim();
        customer.PhoneNumber = ReservationService.NormalizePhone(model.PhoneNumber);
        await _db.SaveChangesAsync();

        var user = await _users.GetUserAsync(User);
        if (user != null)
        {
            user.PhoneNumber = customer.PhoneNumber;
            await _users.UpdateAsync(user);
        }

        TempData["Success"] = "Your details have been updated.";
        return RedirectToAction(nameof(Details));
    }

    [HttpGet, Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> ChangeEmail()
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return NotFound();

        ViewBag.CurrentEmail = user.Email;
        return View(new ChangeEmailViewModel());
    }

    [HttpPost, Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> ChangeEmail(ChangeEmailViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return NotFound();

        ViewBag.CurrentEmail = user.Email;
        if (!ModelState.IsValid) return View(model);

        if (!await _users.CheckPasswordAsync(user, model.CurrentPassword))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "That password is not correct.");
            return View(model);
        }

        // The new address must not belong to another login or to another guest record.
        var newEmail = model.NewEmail.Trim();
        var lower = newEmail.ToLower();
        var other = await _users.FindByEmailAsync(newEmail);
        var inUse = (other != null && other.Id != user.Id)
                    || await _db.Customers.AnyAsync(c => c.Email.ToLower() == lower && c.UserId != user.Id);
        if (inUse)
        {
            ModelState.AddModelError(nameof(model.NewEmail), "That email address is already in use.");
            return View(model);
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id);
        if (customer == null) return NotFound();

        // The email is also the login name, so the login and the guest record change together or not at all.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        customer.Email = newEmail;
        var result = await _users.SetEmailAsync(user, newEmail);
        if (result.Succeeded) result = await _users.SetUserNameAsync(user, newEmail);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            _db.ChangeTracker.Clear();
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        await transaction.CommitAsync();

        await _signIn.RefreshSignInAsync(user);   // keep them logged in under the new email
        TempData["Success"] = "Your email address has been changed. Use it the next time you log in.";
        return RedirectToAction(nameof(Details));
    }

    [HttpGet, Authorize(Roles = RoleNames.Member)]
    public IActionResult DeleteAccount() => View(new DeleteAccountViewModel());

    [HttpPost, Authorize(Roles = RoleNames.Member)]
    public async Task<IActionResult> DeleteAccount(DeleteAccountViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user == null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        if (!await _users.CheckPasswordAsync(user, model.CurrentPassword))
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "That password is not correct.");
            return View(model);
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        if (customer != null)
        {
            // Cancel upcoming bookings so no table is held for an account that no longer exists.
            var today = DateOnly.FromDateTime(DateTime.Today);
            var upcoming = await _db.Reservations.Include(r => r.Sitting)
                .Where(r => r.CustomerId == customer.CustomerId && r.Sitting.SittingDate >= today
                            && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
                .ToListAsync();
            foreach (var reservation in upcoming) reservation.Cancel();

            // Keep the booking history for the restaurant's reports, but remove the personal details.
            customer.FirstName = "Deleted";
            customer.LastName = "Member";
            customer.Email = $"deleted-{customer.CustomerId}@deleted.invalid";
            customer.PhoneNumber = "0000000000";
            customer.UserId = null;
            await _db.SaveChangesAsync();
        }

        var result = await _users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            _db.ChangeTracker.Clear();
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        await transaction.CommitAsync();

        await _signIn.SignOutAsync();
        TempData["Success"] = "Your account has been deleted.";
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
