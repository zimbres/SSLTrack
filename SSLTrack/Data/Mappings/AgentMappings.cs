namespace SSLTrack.Data.Mappings;

public class AgentMappings : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasIndex(e => e.Name).IsUnique();
    }
}
