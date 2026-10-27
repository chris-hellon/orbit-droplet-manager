using Microsoft.EntityFrameworkCore;

namespace Orbit.Core.Data;

public class OrbitDbContext(DbContextOptions<OrbitDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ProviderConnection> Connections => Set<ProviderConnection>();
    public DbSet<SshCredential> SshCredentials => Set<SshCredential>();
    public DbSet<ActivityEntry> Activity => Set<ActivityEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Workspace>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasMany(x => x.Users).WithOne(x => x.Workspace!).HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Connections).WithOne(x => x.Workspace!).HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email);
        });

        b.Entity<ProviderConnection>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EncryptedToken).IsRequired();
            e.Property(x => x.TokenLast4).HasMaxLength(8);
            e.Property(x => x.Label).HasMaxLength(200);
            e.Property(x => x.AccountEmail).HasMaxLength(320);
            e.Property(x => x.Provider).HasConversion<int>();
            // one connection per provider per workspace (single-user: one DO token)
            e.HasIndex(x => new { x.WorkspaceId, x.Provider }).IsUnique();
        });

        b.Entity<SshCredential>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Username).HasMaxLength(64).IsRequired();
            e.Property(x => x.EncryptedPrivateKey).IsRequired();
            e.HasIndex(x => x.WorkspaceId).IsUnique();
            e.HasOne(x => x.Workspace).WithMany().HasForeignKey(x => x.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ActivityEntry>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Category).HasMaxLength(32).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            e.Property(x => x.Message).HasMaxLength(500).IsRequired();
            e.HasIndex(x => new { x.WorkspaceId, x.TimestampUtc });
        });

        b.Entity<Workspace>().HasData(new Workspace
        {
            Id = Seed.WorkspaceId,
            Name = "My Fleet",
            CreatedAt = Seed.Epoch,
        });

        b.Entity<User>().HasData(new User
        {
            Id = Seed.OwnerUserId,
            WorkspaceId = Seed.WorkspaceId,
            Email = "owner@orbit.local",
            DisplayName = "Owner",
            IsOwner = true,
            CreatedAt = Seed.Epoch,
        });
    }
}
