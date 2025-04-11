using System.Text.Json;
using CountingBot.Database.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CountingBot.Database
{
    /// <summary>
    /// Database context for the CountingBot application.
    /// Manages database connections and entity configurations.
    /// Contains all database relationships and mappings for the application.
    /// </summary>
    public class BotDbContext : DbContext
    {
        private static readonly string ConnectionString =
            "Host=localhost;Database=CountingBotDb;Username=Subaka;Password=Subaka1@;Maximum Pool Size=128;Minimum Pool Size=5;";

        public DbSet<GuildSettings> GuildSettings { get; set; }
        public DbSet<ChannelSettings> ChannelSettings { get; set; }
        public DbSet<UserInformation> UserInformation { get; set; }

        // Normalized tables for better performance
        public DbSet<CountingStatsEntity> CountingStats { get; set; }
        public DbSet<UnlockedAchievementEntity> UnlockedAchievements { get; set; }

        // Default constructor for use with dependency injection
        public BotDbContext() { }

        // Constructor that accepts options for more flexible configuration
        public BotDbContext(DbContextOptions<BotDbContext> options)
            : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Configure connection pooling for better performance
                optionsBuilder.UseNpgsql(
                    ConnectionString,
                    options =>
                    {
                        options.MaxBatchSize(100);
                        options.EnableRetryOnFailure(3);
                        options.CommandTimeout(30);
                    }
                );
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var converter = new ValueConverter<Dictionary<ulong, CountingStats>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v =>
                    JsonSerializer.Deserialize<Dictionary<ulong, CountingStats>>(
                        v,
                        (JsonSerializerOptions?)null
                    ) ?? new Dictionary<ulong, CountingStats>()
            );

            var achievementsConverter = new ValueConverter<List<UnlockedAchievementData>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v =>
                    JsonSerializer.Deserialize<List<UnlockedAchievementData>>(
                        v,
                        (JsonSerializerOptions?)null
                    ) ?? new()
            );

            var permissionConverter = new ValueConverter<
                Dictionary<string, CommandPermissionData>,
                string
            >(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v =>
                    JsonSerializer.Deserialize<Dictionary<string, CommandPermissionData>>(
                        v,
                        (JsonSerializerOptions?)null
                    ) ?? new()
            );

            // Configure existing JSON properties
            modelBuilder
                .Entity<UserInformation>()
                .Property(ui => ui.CountingData)
                .HasConversion(converter)
                .HasColumnName("CountingDataJson")
                .HasColumnType("jsonb");

            modelBuilder
                .Entity<UserInformation>()
                .Property(ui => ui.UnlockedAchievements)
                .HasConversion(achievementsConverter)
                .HasColumnName("UnlockedAchievementsJson")
                .HasColumnType("jsonb");

            modelBuilder
                .Entity<GuildSettings>()
                .Property(g => g.CommandPermissions)
                .HasConversion(permissionConverter)
                .HasColumnName("CommandPermissions")
                .HasColumnType("jsonb");

            // Add indexes for better query performance
            modelBuilder.Entity<UserInformation>().HasIndex(ui => ui.UserId);

            modelBuilder.Entity<GuildSettings>().HasIndex(g => g.GuildId);

            modelBuilder.Entity<ChannelSettings>().HasIndex(c => c.ChannelId);

            modelBuilder
                .Entity<ChannelSettings>()
                .HasIndex(c => new { c.GuildId, c.ChannelId })
                .IsUnique();

            // Configure new normalized entities
            modelBuilder
                .Entity<CountingStatsEntity>()
                .HasIndex(c => new { c.UserId, c.GuildId })
                .IsUnique();

            modelBuilder
                .Entity<CountingStatsEntity>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder
                .Entity<UnlockedAchievementEntity>()
                .HasIndex(a => new { a.UserId, a.AchievementId })
                .IsUnique();

            modelBuilder
                .Entity<UnlockedAchievementEntity>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }
    }
}
