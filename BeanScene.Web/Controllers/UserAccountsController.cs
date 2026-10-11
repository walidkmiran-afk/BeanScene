using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Controllers;

/// <summary>Managers look after the Staff and Member logins: create staff, edit details, disable, reset passwords.</summary>
[Authorize(Roles = RoleNames.Manager)]
public class UserAccountsController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;

    public UserAccountsController(UserManager<ApplicationUser> users, ApplicationDbContext db)
    {
        _users = users;
        _db = db;
    }

    // ---- list -------------------------------------------------------------------

    public async Task<IActionResult> Index()
    {
        var rows = new List<AccountRowViewModel>();
        foreach (var role in new[] { RoleNames.Staff, RoleNames.Member })
        {
            var inRole = await _users.GetUsersInRoleAsync(role);
            var ids = inRole.Select(u => u.Id).ToList();
            var customers = await _db.Customers.AsNoTracking()
                .Where(c => c.UserId != null && ids.Contains(c.UserId!)).ToListAsync();

            foreach (var u in inRole)
            {
                var c = customers.FirstOrDefault(x => x.UserId == u.Id);
                rows.Add(new AccountRowViewModel
                {
                    UserId = u.Id,
                    FullName = c?.FullName ?? u.Email ?? "",
                    Email = u.Email ?? "",
                    PhoneNumber = c?.PhoneNumber ?? "",
                    Role = role,
                    IsDisabled = IsDisabled(u)
                });
            }
        }
        return View(rows.OrderBy(r => r.Role).ThenBy(r => r.FullName).ToList());
    }

    // ---- create a staff account ---------------------------------------------------

    [HttpGet]
    public IActionResult Create() => View(new CreateStaffViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var email = model.Email.Trim();
        var lower = email.ToLower();
        if (await _db.Customers.AnyAsync(c => c.Email.ToLower() == lower) || await _users.FindByEmailAsync(email) != null)
        {
            ModelState.AddModelError(nameof(model.Email), "That email address is already in use.");
            return View(model);
        }

        var phone = ReservationService.NormalizePhone(model.PhoneNumber ?? "");
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, PhoneNumber = phone };
        var create = await _users.CreateAsync(user, model.Password);
        if (!create.Succeeded)
        {
            foreach (var error in create.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        try
        {
            var role = await _users.AddToRoleAsync(user, RoleNames.Staff);
            if (!role.Succeeded) throw new InvalidOperationException("Could not add the Staff role.");

            _db.Customers.Add(new Customer
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = email,
                PhoneNumber = phone,
                UserId = user.Id
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception)
        {
            await _users.DeleteAsync(user);   // do not leave a login without a guest record
            ModelState.AddModelError(string.Empty, "The account could not be created. Please try again.");
            return View(model);
        }

        TempData["Success"] = $"Staff account created for {email}.";
        return RedirectToAction(nameof(Index));
    }

    // ---- edit name, phone and email ------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await FindManagedUserAsync(id);
        if (user == null) return NotFound();

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == user.Id);
        if (customer == null) return NotFound();

        return View(new EditAccountViewModel
        {
            UserId = user.Id,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Email = user.Email ?? "",
            PhoneNumber = customer.PhoneNumber
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(EditAccountViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await FindManagedUserAsync(model.UserId);
        if (user == null) return NotFound();
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id);
        if (customer == null) return NotFound();

        // The new email must not belong to another login or another guest record.
        var newEmail = model.Email.Trim();
        var lower = newEmail.ToLower();
        var other = await _users.FindByEmailAsync(newEmail);
        var inUse = (other != null && other.Id != user.Id)
                    || await _db.Customers.AnyAsync(c => c.Email.ToLower() == lower && c.UserId != user.Id);
        if (inUse)
        {
            ModelState.AddModelError(nameof(model.Email), "That email address is already in use.");
            return View(model);
        }

        var phone = ReservationService.NormalizePhone(model.PhoneNumber ?? "");

        // The login and the guest record change together or not at all.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        customer.FirstName = model.FirstName.Trim();
        customer.LastName = model.LastName.Trim();
        customer.PhoneNumber = phone;
        customer.Email = newEmail;
        user.PhoneNumber = phone;

        var result = IdentityResult.Success;
        if (user.Email != newEmail)
        {
            result = await _users.SetEmailAsync(user, newEmail);
            if (result.Succeeded) result = await _users.SetUserNameAsync(user, newEmail);
        }
        if (result.Succeeded) result = await _users.UpdateAsync(user);

        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            _db.ChangeTracker.Clear();
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        await transaction.CommitAsync();

        TempData["Success"] = "Account details updated.";
        return RedirectToAction(nameof(Index));
    }

    // ---- disable / enable ---------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> ToggleDisabled(string id)
    {
        var user = await FindManagedUserAsync(id);
        if (user == null) return NotFound();

        if (IsDisabled(user))
        {
            await _users.SetLockoutEndDateAsync(user, null);
            await _users.ResetAccessFailedCountAsync(user);
            TempData["Success"] = $"{user.Email} is enabled again.";
        }
        else
        {
            await _users.SetLockoutEnabledAsync(user, true);
            await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await _users.UpdateSecurityStampAsync(user);   // ends their current login the next time it is checked
            TempData["Success"] = $"{user.Email} is disabled and can no longer log in.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ---- reset password -----------------------------------------------------------

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await FindManagedUserAsync(id);
        if (user == null) return NotFound();

        ViewBag.Email = user.Email;
        return View(new ResetPasswordViewModel { UserId = user.Id });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        var user = await FindManagedUserAsync(model.UserId);
        if (user == null) return NotFound();

        ViewBag.Email = user.Email;
        if (!ModelState.IsValid) return View(model);

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _users.ResetAccessFailedCountAsync(user);
        TempData["Success"] = $"Password reset for {user.Email}. Tell them the new password in person or by phone.";
        return RedirectToAction(nameof(Index));
    }

    // ---- helpers ------------------------------------------------------------------

    private static bool IsDisabled(ApplicationUser user) =>
        user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;

    /// <summary>Only Staff and Member logins can be managed here. Manager accounts are never returned.</summary>
    private async Task<ApplicationUser?> FindManagedUserAsync(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var user = await _users.FindByIdAsync(id);
        if (user == null) return null;

        if (await _users.IsInRoleAsync(user, RoleNames.Staff)) return user;
        if (await _users.IsInRoleAsync(user, RoleNames.Member)) return user;
        return null;
    }
}