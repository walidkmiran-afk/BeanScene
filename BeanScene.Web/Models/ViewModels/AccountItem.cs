namespace BeanScene.Web.Models.ViewModels;

/// <summary>One row on the Accounts page: the list columns plus what the inline Edit panel needs.</summary>
public class AccountItem : AccountRowViewModel
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public bool IsStaff { get; set; }
}