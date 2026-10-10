using Microsoft.AspNetCore.Identity;

namespace BeanScene.Web.Models;

/// <summary>Inherits IdentityUser (inheritance). Email is the username.</summary>
public class ApplicationUser : IdentityUser
{
    public virtual Customer? Customer { get; set; }
}

public class ApplicationRole : IdentityRole
{
    public ApplicationRole() { }
    public ApplicationRole(string roleName) : base(roleName) { }
}
