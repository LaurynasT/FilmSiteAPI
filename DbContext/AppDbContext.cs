using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace NetRefreshTokenDemo.Api.Models;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TokenInfo> TokenInfos { get; set; }
    public DbSet<FavoriteMedia> Favorites { get; set; }
    public DbSet<WatchListItem> WatchList { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<FavoriteMedia>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId);
        
        modelBuilder.Entity<FavoriteMedia>()
            .HasIndex(f => new { f.UserId, f.MediaId, f.MediaType })
            .IsUnique();
        
        
        modelBuilder.Entity<WatchListItem>()
            .HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId);
        
        modelBuilder.Entity<WatchListItem>()
            .HasIndex(w => new { w.UserId, w.MediaId, w.MediaType })
            .IsUnique();
    }
}