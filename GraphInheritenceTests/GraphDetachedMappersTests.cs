using Detached.Mappers.EntityFramework;
using GraphInheritenceTests.ComplexModels;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace GraphInheritenceTests
{
    [TestFixture]
    public class GraphDetachedMappersTests
    {
        private Customer _superCustomer;
        private Tag _tag2;

        [SetUp]
        public void BeforeEachTest()
        {
            using (var dbContext = new ComplexDbContext())
            {
                dbContext.Database.EnsureDeleted();
                dbContext.Database.EnsureCreated();
            }

            SeedCustomerKindsEnum();

            SeedCountry();

            var addressIngolstadt = new Address()
            {
                Street = "Hauptstraﬂe",
                PostalCode = "85049",
                City = "Ingolstadt",
                //Country = countryDE // Problem by adding with ef as expected - must be by key
                CountryId = 1
            };

            var addressMunich = new Address()
            {
                Street = "Terminalstraﬂe Mitte",
                PostalCode = "85445",
                City = "Oberding",
                CountryId = 1
            };

            var tag1 = new Tag() { Name = "SuperPlus" };
            _tag2 = new Tag() { Name = "Marketing Campaign1" };

            _superCustomer = new Customer()
            {
                CustomerKindId = CustomerKindId.Company,
                CustomerName = "Super Customer",
                PrimaryAddress = addressIngolstadt,
                ShipmentAddress = addressMunich,
            };
            _superCustomer.Tags.Add(tag1);
            _superCustomer.Tags.Add(_tag2);

            using (var dbContext = new ComplexDbContext())
            {
                dbContext.Customers.Add(_superCustomer);

                dbContext.SaveChanges();
            }

            // Save again with plain EF without any changes
            using (var dbContext = new ComplexDbContext())
            {
                dbContext.Update(_superCustomer);
                // No exception expected - OK
                dbContext.SaveChanges();
            }
        }

        [Test]
        public void _1_DoSimpleChangeOnAggregationCustomerKind()
        {
            DoSimpleChangeOnAggregationCustomerKind(_superCustomer);
        }

        [Test]
        public void _2_AChangeOnAggregationCountryShouldBeIgnored()
        {
            AChangeOnAggregationCountryShouldBeIgnored(_superCustomer);
        }

        [Test]
        public void _3_RemoveChangeAndAddEntriesInTagListComposition()
        {
            RemoveChangeAndAddEntriesInTagListComposition(_superCustomer, _tag2.Id);
        }

        [Test]
        public void _4_DoChangeOnCompositionOrganizationNotesWithBackReferenceOrganizationId()
        {
            DoChangeOnCompositionOrganizationNotesWithBackReferenceOrganization(_superCustomer);
        }

        [Test]
        public void _5_DoChangeOnParentChildrenTreeOrHierarchy()
        {
            DoChangeOnParentChildrenTreeOrHierarchy(); //doesn't work - parent gets lost (removed)
        }

        private static void SeedCustomerKindsEnum()
        {
            using (var dbContext = new ComplexDbContext())
            {
                foreach (CustomerKindId customerKindId in Enum.GetValues<CustomerKindId>())
                {
                    CustomerKind customerKind = new CustomerKind() { Id = customerKindId, Name = customerKindId.GetFriendlyName() };
                    dbContext.CustomerKinds.Add(customerKind);
                }

                dbContext.SaveChanges();
            }
        }

        private static void SeedCountry()
        {
            Country countryDE = new Country()
            {
                Name = "Germany",
                IsoCode = "DE",
            };

            using (var dbContext = new ComplexDbContext())
            {
                dbContext.Countries.Add(countryDE);

                dbContext.SaveChanges();
            }
        }

        private void RemoveChangeAndAddEntriesInTagListComposition(Customer superCustomer, int tag2Id)
        {
            superCustomer.Tags = new List<Tag>();
            // Tag1 removed - will not be sent back by client
            superCustomer.Tags.Add(new Tag() { Id = tag2Id, Name = "Changed Marketing Campaign1" });
            superCustomer.Tags.Add(new Tag() { Id = 0, Name = "new Tag" });

            using (var dbContext = new ComplexDbContext())
            {
                //dbContext.Update(superCustomer); // doesn't support change tracking with removed items as we know

                // Leonardo Porro suggests to use an anonymous type 
                var mapped = dbContext.Map<OrganizationBase>(new
                {
                    superCustomer.Id,
                    superCustomer.OrganizationType,
                    superCustomer.Tags
                });
                dbContext.SaveChanges();
            }
            using (var dbContext = new ComplexDbContext())
            {
                var allOrganizations = dbContext.Organizations.Include(c => c.Tags).ToList();
                Assert.That(allOrganizations, Has.Count.EqualTo(1), "Customer gets lost (removed)?!");

                Customer loadedSuperCustomer = allOrganizations.OfType<Customer>().Single(c => c.CustomerName.Contains("Super Customer"));
                Assert.That(loadedSuperCustomer.Tags, Has.Count.EqualTo(2));
                Assert.That(loadedSuperCustomer.Tags.Select(t => t.Name), Does.Not.Contains("Marketing Campaign1"));
                Assert.That(loadedSuperCustomer.Tags.Select(t => t.Name), Contains.Item("Changed Marketing Campaign1"));
                Assert.That(loadedSuperCustomer.Tags.Select(t => t.Name), Contains.Item("new Tag"));
            }
        }

        private static void DoSimpleChangeOnAggregationCustomerKind(Customer superCustomer)
        {
            superCustomer.CustomerName = "Super Customer - Changed to incomplete private";

            // the following in combination works, too :-)
            // superCustomer.CustomerKindId = CustomerKindId.Private;
            // superCustomer.CustomerKind = new CustomerKind() { Id = CustomerKindId.Private };

            superCustomer.CustomerKind = new CustomerKind() { Id = CustomerKindId.Private };

            using (var dbContext = new ComplexDbContext())
            {
                // if you use Map<Customer>(superCustomer):
                // Detached.Mappers.Exceptions.MapperException : Customer is not a valid value for discriminator in entity GraphInheritenceTests.ComplexModels.Customer.

                // base type OrganizationBase (whether it's not logical correct) works partly, but ignores the Customer specific properties.
                var mapped = dbContext.Map<Customer>(superCustomer);

                dbContext.SaveChanges();
            }

            using (var dbContext = new ComplexDbContext())
            {
                var loadedCustomer = dbContext.Customers
                    .Include(c => c.PrimaryAddress)
                    .Include(c => c.ShipmentAddress)
                    .Include(c => c.Tags)
                    .Include(c => c.CustomerKind)
                    .First();
                Assert.That(loadedCustomer.CustomerName, Is.EqualTo("Super Customer - Changed to incomplete private"), "No change would be saved. Maybe it's because it exists only in the concrete type.");
                Assert.That(loadedCustomer.CustomerKind.Id, Is.EqualTo(CustomerKindId.Private));
                Assert.That(loadedCustomer.CustomerKind.Name, Is.EqualTo("Private Customer"));
                Assert.That(loadedCustomer.PrimaryAddress.City, Is.EqualTo("Ingolstadt"));
                Assert.That(loadedCustomer.ShipmentAddress.City, Is.EqualTo("Oberding"));
                Assert.That(loadedCustomer.Tags, Has.Count.EqualTo(2));
                Assert.That(loadedCustomer.Tags.Select(t => t.Name), Contains.Item("SuperPlus"));
                Assert.That(loadedCustomer.Tags.Select(t => t.Name), Contains.Item("Marketing Campaign1"));
            }
        }

        private static void DoChangeOnCompositionOrganizationNotesWithBackReferenceOrganization(Customer superCustomer)
        {
            // Back-references don't work - OrganizationId should be included in DTO's
            superCustomer.Notes.Add(new OrganizationNotes()
            {
                Date = DateTime.Today,
                Text = "Note...",
                //OrganizationId = superCustomer.Id // is allowed in entity, but mustn't be set from Frontend
            });

            using (var dbContext = new ComplexDbContext())
            {
                var mapped = dbContext.Map<OrganizationBase>(superCustomer);

                dbContext.SaveChanges();
            }

            using (var dbContext = new ComplexDbContext())
            {
                var superCustomerLoaded = dbContext.Customers
                    .Include(c => c.Notes)
                    .Single(c => c.Id == superCustomer.Id);

                Assert.That(superCustomerLoaded.Notes, Has.Count.EqualTo(1));
                Assert.That(superCustomerLoaded.Notes[0].Date, Is.EqualTo(DateTime.Today));
                Assert.That(superCustomerLoaded.Notes[0].Text, Is.EqualTo("Note..."));
                Assert.That(superCustomerLoaded.Notes[0].OrganizationId, Is.EqualTo(superCustomer.Id));
                Assert.That(superCustomerLoaded.Notes[0].Organization.Id, Is.EqualTo(superCustomer.Id));
            }
        }

        private static void DoChangeOnParentChildrenTreeOrHierarchy()
        {
            // Seed first
            Customer parent = new Customer() { Name = "Parent", PrimaryAddressId = 1, CustomerKindId = CustomerKindId.Company };
            Customer child = new Customer() { Name = "Child", PrimaryAddressId = 1, CustomerKindId = CustomerKindId.Company };
            Customer childChild = new Customer() { Parent = child, Name = "ChildChild", PrimaryAddressId = 1, CustomerKindId = CustomerKindId.Company };
            child.Children.Add(childChild);
            using (var dbContext = new ComplexDbContext())
            {
                var mapped1 = dbContext.Add<Customer>(parent);
                var mapped2 = dbContext.Add<Customer>(child);
                var mapped3 = dbContext.Add<Customer>(childChild);

                dbContext.SaveChanges();
            }

            child.ParentId = parent.Id;
            // Link tree as aggregation
            //child.Parent = new Customer { Id = parent.Id };
            parent.Children.Add(child);

            using (var dbContext = new ComplexDbContext())
            {
                //dbContext.Update(parent); // works - expected
                //dbContext.Update(child); // works - expected

                // if the whole entities are used - parent gets lost (removed)
                // so the workaround with anonymous type is working
                var mapped1 = dbContext.Map<OrganizationBase>(new
                {
                    parent.Id,
                    parent.OrganizationType,
                    parent.ParentId,
                    parent.Parent,
                    parent.Children
                });
                var mapped2 = dbContext.Map<OrganizationBase>(new
                {
                    child.Id,
                    child.OrganizationType,
                    child.ParentId,
                    child.Parent,
                    child.Children
                });

                dbContext.SaveChanges();
            }

            using (var dbContext = new ComplexDbContext())
            {
                var allCustomers = dbContext.Customers;
                var loadedHierarchy = allCustomers
                    .Include(c => c.Parent)
                    .Include(c => c.Children)
                    .AsEnumerable();

                Assert.That(loadedHierarchy.Select(c => c.Name), Contains.Item("Parent"), "Parent gets lost (removed)?!");
                Customer parentLoaded = loadedHierarchy.Single(c => c.Name == "Parent");

                Assert.That(parentLoaded.Children, Has.Count.EqualTo(1));
                Assert.That(parentLoaded.Children[0].Name, Is.EqualTo("Child"));
                Assert.That(parentLoaded.Children[0].Children, Has.Count.EqualTo(1));
                Assert.That(parentLoaded.Children[0].Children[0].Name, Is.EqualTo("ChildChild"));
            }
        }

        private static void AChangeOnAggregationCountryShouldBeIgnored(Customer superCustomer)
        {
            superCustomer.PrimaryAddress = new Address() { Id = 1, Country = new Country() { Name = "changed" } };

            using (var dbContext = new ComplexDbContext())
            {
                // preferred suggested way
                var mapped = dbContext.Map<OrganizationBase>(new
                {
                    superCustomer.Id,
                    superCustomer.OrganizationType,
                    superCustomer.PrimaryAddress
                });

                // works, too
                // var mapped = dbContext.Map<OrganizationBase>(superCustomer);

                dbContext.SaveChanges();
            }

            using (var dbContext = new ComplexDbContext())
            {
                Assert.That(dbContext.Countries.Count, Is.EqualTo(1));

                var germanyReLoaded = dbContext.Countries.First();
                // country as aggregate shouldn't be changed
                Assert.That(germanyReLoaded.Name, Is.Not.EqualTo("changed"));
                Assert.That(germanyReLoaded.Name, Is.EqualTo("Germany"));
            }
        }
    }
}