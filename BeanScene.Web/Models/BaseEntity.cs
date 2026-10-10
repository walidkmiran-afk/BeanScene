using System.ComponentModel.DataAnnotations;

namespace BeanScene.Web.Models;

/// <summary>Interface implemented by every entity that records when it was created and last changed.</summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Abstract base class (inheritance). Holds the audit fields and the concurrency token.
/// ApplicationDbContext.SaveChanges stamps these values automatically.
/// </summary>
public abstract class BaseEntity : IAuditable
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token. A new value is generated on every insert and update.</summary>
    [ConcurrencyCheck]
    public Guid RowVersion { get; set; } = Guid.NewGuid();
}
