using Microsoft.EntityFrameworkCore;
using LogViewer.Api.Models;

namespace LogViewer.Api.Data;

/// <summary>
/// 数据库上下文
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ErrorLog> ErrorLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ErrorLog>(entity =>
        {
            // 表名
            entity.ToTable("error_logs");

            // 索引
            entity.HasIndex(e => e.Level).HasDatabaseName("idx_level");
            entity.HasIndex(e => e.ClusterName).HasDatabaseName("idx_cluster_name");
            entity.HasIndex(e => e.NodeName).HasDatabaseName("idx_node_name");
            entity.HasIndex(e => e.Tag).HasDatabaseName("idx_tag");
            entity.HasIndex(e => e.TimeMs).HasDatabaseName("idx_time_ms");

            // 复合索引：常用查询组合
            entity.HasIndex(e => new { e.Level, e.ClusterName, e.NodeName, e.TimeMs })
                  .HasDatabaseName("idx_composite");
        });
    }
}
