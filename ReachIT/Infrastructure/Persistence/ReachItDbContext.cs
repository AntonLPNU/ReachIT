// EF Core DbContext for ReachIT local persistence.
using Microsoft.EntityFrameworkCore;
using ReachIT.Domain.Models;

namespace ReachIT.Infrastructure.Persistence;

public sealed class ReachItDbContext : DbContext
{
    public ReachItDbContext(DbContextOptions<ReachItDbContext> options)
        : base(options)
    {
    }

    public DbSet<ProjectMeta> Projects => Set<ProjectMeta>();
    public DbSet<ProjectItem> ProjectItems => Set<ProjectItem>();
    public DbSet<ProjectTreeNode> ProjectTreeNodes => Set<ProjectTreeNode>();
    public DbSet<ExternalResourceItem> ExternalResources => Set<ExternalResourceItem>();
    public DbSet<RecentExternalFileItem> RecentExternalFiles => Set<RecentExternalFileItem>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskCategory> TaskCategories => Set<TaskCategory>();
    public DbSet<TaskTag> TaskTags => Set<TaskTag>();
    public DbSet<TaskHistoryEntry> TaskHistoryEntries => Set<TaskHistoryEntry>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<FocusSession> FocusSessions => Set<FocusSession>();
    public DbSet<ProductivityStat> ProductivityStats => Set<ProductivityStat>();
    public DbSet<User> Users => Set<User>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkUnit> WorkUnits => Set<WorkUnit>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<TaskSuggestion> TaskSuggestions => Set<TaskSuggestion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ProjectMeta>().HasKey(x => x.Id);
        modelBuilder.Entity<ProjectItem>().HasKey(x => x.Id);
        modelBuilder.Entity<ProjectTreeNode>().HasKey(x => x.Id);
        modelBuilder.Entity<ExternalResourceItem>().HasKey(x => x.Id);
        modelBuilder.Entity<RecentExternalFileItem>().HasKey(x => x.Id);
        modelBuilder.Entity<TaskItem>().HasKey(x => x.Id);
        modelBuilder.Entity<TaskCategory>().HasKey(x => x.Id);
        modelBuilder.Entity<TaskTag>().HasKey(x => x.Id);
        modelBuilder.Entity<TaskHistoryEntry>().HasKey(x => x.Id);
        modelBuilder.Entity<AppSettings>().HasKey(x => x.Id);
        modelBuilder.Entity<FocusSession>().HasKey(x => x.Id);
        modelBuilder.Entity<ProductivityStat>().HasKey(x => x.Id);
        modelBuilder.Entity<User>().HasKey(x => x.Id);
        modelBuilder.Entity<WorkItem>().HasKey(x => x.Id);
        modelBuilder.Entity<WorkUnit>().HasKey(x => x.Id);
        modelBuilder.Entity<Milestone>().HasKey(x => x.Id);
        modelBuilder.Entity<TaskSuggestion>().HasKey(x => x.Id);

        modelBuilder.Entity<ProjectMeta>().Property(x => x.ProjectName).HasMaxLength(200).IsRequired();
        modelBuilder.Entity<ProjectMeta>().Property(x => x.Description).HasMaxLength(2000);
        modelBuilder.Entity<ProjectMeta>().Property(x => x.ProjectDirectoryPath).HasMaxLength(1500).IsRequired();
        modelBuilder.Entity<ProjectMeta>().Property(x => x.RitFilePath).HasMaxLength(1000);
        modelBuilder.Entity<ProjectItem>().Property(x => x.Name).HasMaxLength(260).IsRequired();
        modelBuilder.Entity<ProjectItem>().Property(x => x.RelativePath).HasMaxLength(1500).IsRequired();
        modelBuilder.Entity<ProjectItem>().Property(x => x.ItemType).HasConversion<int>().IsRequired();

        modelBuilder.Entity<ProjectTreeNode>().Property(x => x.Name).HasMaxLength(260).IsRequired();
        modelBuilder.Entity<ProjectTreeNode>().Property(x => x.FullPath).HasMaxLength(2500).IsRequired();
        modelBuilder.Entity<ProjectTreeNode>().Property(x => x.RelativePath).HasMaxLength(2500);
        modelBuilder.Entity<ProjectTreeNode>().Property(x => x.NodeType).HasConversion<int>().IsRequired();
        modelBuilder.Entity<ProjectTreeNode>().Property(x => x.ExternalTargetPathOrUrl).HasMaxLength(2500);
        modelBuilder.Entity<ProjectTreeNode>()
            .HasMany(x => x.Children)
            .WithOne()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExternalResourceItem>().Property(x => x.DisplayName).HasMaxLength(260).IsRequired();
        modelBuilder.Entity<ExternalResourceItem>().Property(x => x.SourcePathOrUrl).HasMaxLength(2500).IsRequired();
        modelBuilder.Entity<ExternalResourceItem>().Property(x => x.StoredPath).HasMaxLength(2500);
        modelBuilder.Entity<ExternalResourceItem>().Property(x => x.ResourceType).HasConversion<int>().IsRequired();
        modelBuilder.Entity<ExternalResourceItem>().Property(x => x.AttachMode).HasConversion<int>().IsRequired();

        modelBuilder.Entity<RecentExternalFileItem>().Property(x => x.DisplayName).HasMaxLength(260).IsRequired();
        modelBuilder.Entity<RecentExternalFileItem>().Property(x => x.SourcePathOrUrl).HasMaxLength(2500).IsRequired();
        modelBuilder.Entity<RecentExternalFileItem>().Property(x => x.ResourceType).HasConversion<int>().IsRequired();

        modelBuilder.Entity<TaskItem>().Property(x => x.Title).HasMaxLength(250).IsRequired();
        modelBuilder.Entity<TaskTag>().Property(x => x.Value).HasMaxLength(100).IsRequired();
        modelBuilder.Entity<TaskItem>()
            .HasMany(x => x.Tags)
            .WithOne()
            .HasForeignKey(x => x.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<WorkItem>().Property(x => x.Title).HasMaxLength(250).IsRequired();
        modelBuilder.Entity<WorkItem>().Property(x => x.Description).HasMaxLength(4000);
        modelBuilder.Entity<WorkItem>().Property(x => x.Type).HasConversion<int>().IsRequired();
        modelBuilder.Entity<WorkItem>().Property(x => x.Status).HasConversion<int>().IsRequired();
        modelBuilder.Entity<WorkItem>().Property(x => x.LinkedPath).HasMaxLength(2500);
        modelBuilder.Entity<WorkItem>().Property(x => x.LinkedApp).HasMaxLength(260);
        modelBuilder.Entity<WorkItem>().Property(x => x.Tags).HasMaxLength(1000);
        modelBuilder.Entity<WorkItem>().Property(x => x.Notes).HasMaxLength(8000);
        modelBuilder.Entity<WorkItem>()
            .HasMany(x => x.Children)
            .WithOne(x => x.Parent)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<WorkItem>()
            .HasIndex(x => new { x.ProjectId, x.LegacyTaskItemId });
        modelBuilder.Entity<WorkItem>()
            .HasIndex(x => new { x.ProjectId, x.LinkedPath });

        modelBuilder.Entity<WorkUnit>().Property(x => x.Type).HasConversion<int>().IsRequired();
        modelBuilder.Entity<WorkUnit>().Property(x => x.Source).HasMaxLength(100);
        modelBuilder.Entity<WorkUnit>().Property(x => x.MetadataJson).HasMaxLength(8000);
        modelBuilder.Entity<WorkUnit>().HasIndex(x => new { x.ProjectId, x.WorkItemId });
        modelBuilder.Entity<WorkUnit>().HasIndex(x => new { x.ProjectId, x.CreatedAt });

        modelBuilder.Entity<Milestone>().Property(x => x.Title).HasMaxLength(250).IsRequired();
        modelBuilder.Entity<Milestone>().Property(x => x.Description).HasMaxLength(4000);
        modelBuilder.Entity<Milestone>().Property(x => x.Status).HasConversion<int>().IsRequired();

        modelBuilder.Entity<TaskSuggestion>().Property(x => x.SuggestedTitle).HasMaxLength(250).IsRequired();
        modelBuilder.Entity<TaskSuggestion>().Property(x => x.SuggestedDescription).HasMaxLength(4000);
        modelBuilder.Entity<TaskSuggestion>().Property(x => x.SuggestedType).HasConversion<int>().IsRequired();
        modelBuilder.Entity<TaskSuggestion>().Property(x => x.SuggestedLinkedPath).HasMaxLength(2500);
        modelBuilder.Entity<TaskSuggestion>().Property(x => x.Reason).HasMaxLength(1000);
        modelBuilder.Entity<TaskSuggestion>().Property(x => x.Status).HasConversion<int>().IsRequired();
        modelBuilder.Entity<TaskSuggestion>().HasIndex(x => new { x.ProjectId, x.Status });
    }
}
