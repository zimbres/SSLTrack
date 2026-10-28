namespace SSLTrack.Data;

public class ApplicationDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly IConfiguration _configuration;
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IConfiguration configuration) : base(options)
    {
        _configuration = configuration;
    }

    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    public DbSet<ServiceAccount> ServiceAccounts { get; set; } = null!;
    public DbSet<SmtpSettings> SmtpSettings { get; set; } = null!;
    public DbSet<AppUser> Users { get; set; } = null!;
    public DbSet<Domain> Domains
    {
        get; set;
    }
    public DbSet<Agent> Agents
    {
        get; set;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite(_configuration.GetConnectionString("Data"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(x => x.NormalizedEmail).IsUnique();
        modelBuilder.Entity<AppUser>().HasIndex(x => new { x.OidcIssuer, x.OidcSubject }).IsUnique();
        modelBuilder.Entity<ServiceAccount>().HasIndex(x => x.NormalizedEmail).IsUnique();
        modelBuilder.Entity<ServiceAccount>().HasOne<Agent>().WithMany().HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
