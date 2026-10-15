using BeanScene.Web.Data;
using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;
using BeanScene.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BeanScene.Web.Controllers;

/// <summary>Managers look after the Staff and Member logins: create staff, edit details, disable, reset passwords.
/// Everything happens on the one Accounts page; the actions below only save and answer.</summary>
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
        var rows = new List<AccountItem>();
        foreach (var role in new[] { RoleNames.Staff, RoleNames.Member })
        {
            var inRole = await _users.GetUsersInRoleAsync(role);
            var ids = inRole.Select(u => u.Id).ToList();
            var customers = await _db.Customers.AsNoTracking()
                .Where(c => c.UserId != null && ids.Contains(c.UserId!)).ToListAsync();

            foreach (var u in inRole)
            {
                var c = customers.FirstOrDefault(x => x.UserId == u.Id);
                rows.Add(new AccountItem
                {
                    UserId = u.Id,
                    FirstName = c?.FirstName ?? "",
                    LastName = c?.LastName ?? "",
                    FullName = c?.FullName ?? u.Email ?? "",
                    Email = u.Email ?? "",
                    PhoneNumber = c?.PhoneNumber ?? "",
                    Role = role,
                    IsStaff = role == RoleNames.Staff,
                    IsDisabled = IsDisabled(u)
                });
            }
        }
        return View(rows.OrderBy(r => r.FullName).ToList());
    }

    // ---- create a staff account ---------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> Create(CreateStaffViewModel model)
    {
        if (!ModelState.IsValid) return Fail(Errors());

        var email = model.Username.Trim().ToLower() + "@beanscene.test";
        var lower = email.ToLower();
        if (await _db.Customers.AnyAsync(c => c.Email.ToLower() == lower) || await _users.FindByEmailAsync(email) != null)
            return Fail("That username is already in use.");

        var phone = ReservationService.NormalizePhone(model.PhoneNumber ?? "");
        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, PhoneNumber = phone };
        var create = await _users.CreateAsync(user, model.Password);
        if (!create.Succeeded) return Fail(string.Join(" ", create.Errors.Select(e => e.Description)));

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
            return Fail("The account could not be created. Please try again.");
        }

        return Done($"Staff account created. Login: {email}");
    }

    // ---- edit name, phone and login ------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> Edit(EditAccountViewModel model, string? username)
    {
        if (!ModelState.IsValid) return Fail(Errors());

        var user = await FindManagedUserAsync(model.UserId);
        if (user == null) return Fail(Missing);
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id);
        if (customer == null) return Fail(Missing);

        // A staff login is a username plus @beanscene.test, so managers change the username and never the email itself.
        // Members log in with their email, so for them the email is what changes.
        var isStaff = await _users.IsInRoleAsync(user, RoleNames.Staff);
        string newEmail;
        if (isStaff)
        {
            var name = (username ?? "").Trim().ToLower();
            if (name.Length < 3 || name.Length > 40 || !System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z0-9]+(\.[a-z0-9]+)*$"))
                return Fail("The username needs 3 to 40 letters or digits, with single dots between the parts.");
            newEmail = name + "@beanscene.test";
        }
        else
        {
            newEmail = model.Email.Trim();
        }

        // The new login must not belong to another login or another guest record.
        var lower = newEmail.ToLower();
        var other = await _users.FindByEmailAsync(newEmail);
        var inUse = (other != null && other.Id != user.Id)
                    || await _db.Customers.AnyAsync(c => c.Email.ToLower() == lower && c.UserId != user.Id);
        if (inUse) return Fail(isStaff ? "That username is already in use." : "That email address is already in use.");

        var phone = ReservationService.NormalizePhone(model.PhoneNumber ?? "");
        var loginChanged = !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase);

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
            return Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
        await transaction.CommitAsync();

        if (isStaff && loginChanged) return Done($"Username changed. They log in as {newEmail} from now on.");
        return Done("Account details updated.");
    }

    // ---- disable / enable ---------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> ToggleDisabled(string id)
    {
        var user = await FindManagedUserAsync(id);
        if (user == null) return Fail(Missing);

        if (IsDisabled(user))
        {
            await _users.SetLockoutEndDateAsync(user, null);
            await _users.ResetAccessFailedCountAsync(user);
            return Done($"{user.Email} is enabled again.");
        }

        await _users.SetLockoutEnabledAsync(user, true);
        await _users.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        await _users.UpdateSecurityStampAsync(user);   // ends their current login the next time it is checked
        return Done($"{user.Email} is disabled and can no longer log in. Press Enable to undo.");
    }

    // ---- delete -------------------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> Delete(string userId)
    {
        var user = await FindManagedUserAsync(userId);
        if (user == null) return Fail(Missing);

        var email = user.Email;
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.UserId == user.Id);

        await using var transaction = await _db.Database.BeginTransactionAsync();
        if (customer != null)
        {
            // Cancel upcoming bookings so no table is held for an account that no longer exists.
            var today = RestaurantClock.Today;
            var upcoming = await _db.Reservations.Include(r => r.Sitting)
                .Where(r => r.CustomerId == customer.CustomerId && r.Sitting.SittingDate >= today
                            && (r.Status == ReservationStatus.Pending || r.Status == ReservationStatus.Confirmed))
                .ToListAsync();
            foreach (var reservation in upcoming) reservation.Cancel();

            // Keep the booking history for reports, but remove the personal details.
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
            return Fail(string.Join(" ", result.Errors.Select(e => e.Description)));
        }
        await transaction.CommitAsync();

        return Done($"{email} was deleted.");
    }

    // ---- reset password -----------------------------------------------------------

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        var user = await FindManagedUserAsync(model.UserId);
        if (user == null) return Fail(Missing);
        if (!ModelState.IsValid) return Fail(Errors());

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, model.NewPassword);
        if (!result.Succeeded) return Fail(string.Join(" ", result.Errors.Select(e => e.Description)));

        await _users.ResetAccessFailedCountAsync(user);
        return Done($"Password reset for {user.Email}. Tell them the new password in person or by phone.");
    }

    // ---- helpers ------------------------------------------------------------------

    private const string Missing = "That account no longer exists. Refresh the page.";

    /// <summary>The page saves in the background and sends this header; without it the action redirects as before.</summary>
    private bool IsFetch => Request.Headers["X-Requested-With"] == "fetch";

    private IActionResult Done(string message)
    {
        if (IsFetch) return Json(new { ok = true, message });
        TempData["Success"] = message;
        return RedirectToAction(nameof(Index));
    }

    private IActionResult Fail(string message)
    {
        if (IsFetch) return Json(new { ok = false, message });
        TempData["Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    private string Errors()
    {
        var messages = ModelState.Values.SelectMany(v => v.Errors)
            .Select(e => e.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct();
        var text = string.Join(" ", messages);
        return text.Length > 0 ? text : "Please check the details and try again.";
    }

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