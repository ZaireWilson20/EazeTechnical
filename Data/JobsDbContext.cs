using EazeTechnical.Models;
using Microsoft.EntityFrameworkCore;

namespace EazeTechnical.Data;

public class JobsDbContext : DbContext
{
    public DbSet<QueryResponse> JobPostQueries { get; set; }
    private readonly ILogger<JobsDbContext> _logger;

    
    public JobsDbContext(DbContextOptions<JobsDbContext> options, ILogger<JobsDbContext> logger) : base(options)
    {
        _logger = logger;
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QueryResponse>()
            .ToTable("queries")
            .Property(e => e.Results)
            .HasColumnType("jsonb"); // Explicitly set the column type to jsonb
    }
    
}

