namespace MangaERP.BuildingBlocks.Domain.Abstractions;

/// <summary>
/// Base entity with GUID Id. All domain entities should inherit from this.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    protected Entity() { }

    protected Entity(Guid id) => Id = id;

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id == other.Id;
    }

    public static bool operator ==(Entity? left, Entity? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(Entity? left, Entity? right)
        => !(left == right);

    public override int GetHashCode() => Id.GetHashCode();
}
