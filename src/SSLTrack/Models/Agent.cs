namespace SSLTrack.Models;

public class Agent : Entity, IEquatable<Agent>
{
    public Agent()
    {
    }

    public Agent(string name)
    {
        Name = name;
    }

    public string? Name { get; set; }
    public string UserId { get; set; } = "User";

    public bool Equals(Agent? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name;
    }

    public override bool Equals(object? obj) => obj is Agent agent && Equals(agent);

    public override int GetHashCode() => Name.GetHashCode();

    public override string ToString() => Name;
}