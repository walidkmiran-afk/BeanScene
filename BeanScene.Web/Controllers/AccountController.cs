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

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
