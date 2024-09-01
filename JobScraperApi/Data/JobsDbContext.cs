using EazeTechnical.Models;
using Microsoft.EntityFrameworkCore;

namespace EazeTechnical.Data;

public class JobsDbContext : DbContext
{
    private readonly IConfiguration? _configuration;
    public DbSet<JobPosting>? JobPostings;



    public JobsDbContext(){}
    
    public JobsDbContext(IConfiguration? configuration)
    {
        _configuration = configuration;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured && _configuration != null)
        {
            optionsBuilder
                .UseNpgsql(_configuration.GetConnectionString("DefaultConnection"));
        }
        
        base.OnConfiguring(optionsBuilder);
    }

}