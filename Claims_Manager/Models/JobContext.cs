using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;

namespace Claims_Manager.Models
{
    public class JobContext : DbContext
    {


        


        public JobContext(DbContextOptions<JobContext> options)
            : base(options)
        {

        }

        public DbSet<Job> TodoItems { get; set; } = null!;

    }
}