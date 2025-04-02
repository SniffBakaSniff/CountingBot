using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using CountingBot.Database.Models;

namespace CountingBot.Database
{
    public class BotDbContext : DbContext
    {
        public DbSet<GuildSettings> GuildSettings { get; set; }
        public DbSet<ChannelSettings> ChannelSettings { get; set; }
        public DbSet<UserInformation> UserInformation { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql("Host=localhost;Database=CountingBotDb;Username=Subaka;Password=Subaka1@");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var converter = new ValueConverter<Dictionary<ulong, CountingStats>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Dictionary<ulong, CountingStats>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<ulong, CountingStats>());

            modelBuilder.Entity<UserInformation>()
                .Property(ui => ui.CountingData)
                .HasConversion(converter)
                .HasColumnName("CountingDataJson")
                .HasColumnType("jsonb");

            base.OnModelCreating(modelBuilder);
        }
    }
}