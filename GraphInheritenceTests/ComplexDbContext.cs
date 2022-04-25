﻿using Detached.Mappers.EntityFramework;
using GraphInheritenceTests.ComplexModels;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GraphInheritenceTests
{
    public class ComplexDbContext : DbContext
    {
        public DbSet<OrganizationBase> Organizations { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Government> Governments { get; set; }
        public DbSet<Address> Addresses { get; set; }
        public DbSet<Country> Countries { get; set; }
        public DbSet<OrganizationNotes> OrganizationNotes { get; set; }
        public DbSet<CustomerKind> CustomerKinds { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseSqlite("Data Source=ComplexDB.db;")
                .EnableSensitiveDataLogging()
                .LogTo(Console.WriteLine)
                .EnableDetailedErrors()
                .UseDetached(options =>
                {
                    // not what we want, because OrganizationBase can be Customer or Government
                    // but this line is a test to drive it further
                    //options.Configure<OrganizationBase>().Constructor(x => new Customer());

                    options.Configure<OrganizationBase>().Discriminator(o => o.OrganizationType)
                        .Value(nameof(Customer), typeof(Customer))
                        .Value(nameof(Government), typeof(Government));
                });
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OrganizationBase>()
                .HasDiscriminator(o => o.OrganizationType)
                .HasValue<Customer>(nameof(Customer))
                .HasValue<Government>(nameof(Government));

            modelBuilder.Entity<OrganizationBase>()
                .HasOne<Address>(o => o.PrimaryAddress);

            modelBuilder.Entity<OrganizationBase>()
                .HasOne<Address>(o => o.ShipmentAddress);

            // Back-references don't work
            // Exception:
            // System.InvalidOperationException : An error was generated for warning
            // 'Microsoft.EntityFrameworkCore.Query.NavigationBaseIncludeIgnored': The navigation
            // 'OrganizationNotes.Organization' was ignored from 'Include' in the query since the fix-up
            // will automatically populate it. If any further navigations are specified in 'Include' afterwards
            // then they will be ignored. Walking back include tree is not allowed. This exception can be suppressed
            // or logged by passing event ID 'CoreEventId.NavigationBaseIncludeIgnored' to the 'ConfigureWarnings' method
            // in 'DbContext.OnConfiguring' or 'AddDbContext'.
            modelBuilder.Entity<OrganizationBase>()
                .HasMany<OrganizationNotes>(o => o.Notes);
            // .WithOne(n => n.Organization)
            // .HasForeignKey(k => k.OrganizationId);

            // Tree / Hierarchy doesn't work with Map :-(
            modelBuilder.Entity<OrganizationBase>(entity =>
            {
                entity.HasOne(s => s.Parent);
                entity.HasMany(s => s.Children)
                .WithOne()
                .HasForeignKey(s => s.ParentId);
            });
        }

        public override int SaveChanges()
        {
            //var now = DateTime.Now;
            foreach (var changedEntity in ChangeTracker.Entries())
            {
                if (changedEntity.Entity is IdBase entity)
                {
                    switch (changedEntity.State)
                    {
                        case EntityState.Added:
                            //entity.CreatedAt = now;
                            //entity.ModifiedAt = null;
                            break;

                        case EntityState.Modified:
                            //Entry(entity).Property(x => x.CreatedAt).IsModified = false;
                            //entity.ModifiedAt = now;
                            if (entity is OrganizationNotes entity2)
                            {
                                Entry(entity2).State = EntityState.Added;
                            }
                            break;
                    }
                }
            }
            try
            {
                int result = base.SaveChanges();
                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var entries = ex.Entries;
                throw;
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException;
                throw;
            }
        }
    }
}
