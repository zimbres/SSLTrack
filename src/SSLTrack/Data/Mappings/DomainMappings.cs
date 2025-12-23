namespace SSLTrack.Data.Mappings;

public class DomainMappings : IEntityTypeConfiguration<Domain>
{
    public void Configure(EntityTypeBuilder<Domain> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.DomainName);
        builder.Property(e => e.Port);
        builder.Property(e => e.CertCN);
        builder.Property(e => e.Issuer);
        builder.Property(e => e.ExpiryDate);
        builder.Property(e => e.LastChecked);
        builder.Property(e => e.UserId);
        builder.Property(e => e.Agent);
        builder.Property(e => e.Silenced);
        builder.Property(e => e.PublicPrefix);

        builder.HasIndex(e => e.DomainName);
    }
}
