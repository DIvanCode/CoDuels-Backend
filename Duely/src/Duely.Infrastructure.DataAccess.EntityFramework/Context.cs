using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Duely.Domain.Models;
namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class Context : DbContext
{
    public DbSet<Duel> Duels => Set<Duel>();
    public DbSet<Submission> Submissions => Set<Submission>();

    public Context(DbContextOptions<Context> options) : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}