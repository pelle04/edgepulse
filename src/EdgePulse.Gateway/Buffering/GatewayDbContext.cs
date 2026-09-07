using Microsoft.EntityFrameworkCore;

namespace EdgePulse.Gateway.Buffering
{
    internal class GatewayDbContext : DbContext
    {
        public DbSet<ReadingEntity> Readings => Set<ReadingEntity>();

        public GatewayDbContext(DbContextOptions<GatewayDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReadingEntity>(entity =>
            {
                entity.ToTable("Readings");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Id).ValueGeneratedOnAdd();
            });
        }
    }

    // EF Core's Sqlite provider has a built-in DateTimeOffset <-> TEXT converter, unlike
    // Dapper — this is what made the migration off Dapper worth doing (see ADR candidate:
    // Dapper vs. EF Core). No custom value converter needed here.
    internal class ReadingEntity
    {
        public long Id { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public DateTimeOffset TimestampUtc { get; set; }
        public DateTimeOffset? ForwardedAtUtc { get; set; }
    }
}
