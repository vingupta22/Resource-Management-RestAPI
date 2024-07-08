using Microsoft.EntityFrameworkCore;
using System.Diagnostics.CodeAnalysis;
using MongoDB.Driver;
using MongoDB.Bson;

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