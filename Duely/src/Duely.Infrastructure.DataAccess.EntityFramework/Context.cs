﻿using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Duely.Domain.Models;
namespace Duely.Infrastructure.DataAccess.EntityFramework;

public sealed class Context : DbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Duel> Duels => Set<Duel>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Outbox> Outbox => Set<Outbox>();

    public Context(DbContextOptions<Context> options) : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}