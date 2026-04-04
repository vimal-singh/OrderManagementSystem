using Microsoft.EntityFrameworkCore;
using OrderManagementSystem.API.Entities;

namespace OrderManagementSystem.API.Data
{
    public class OrderManagementDbContext(DbContextOptions<OrderManagementDbContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Seed Products
            modelBuilder.Entity<Product>().HasData(
                new Product { Id = 1, Name = "Laptop", Category = "Electronics", Price = 60000, IsActive = true, StockQuantity = 100 },
                new Product { Id = 2, Name = "Smartphone", Category = "Electronics", Price = 25000, IsActive = true, StockQuantity = 100 },
                new Product { Id = 3, Name = "Office Chair", Category = "Furniture", Price = 8000, IsActive = true, StockQuantity = 100 },
                new Product { Id = 4, Name = "Wireless Mouse", Category = "Accessories", Price = 1500, IsActive = true, StockQuantity = 100 }
            );

            // Seed Customers 
            modelBuilder.Entity<Customer>().HasData(
                new Customer { Id = 1, FullName = "Ravi Kumar", Email = "ravi@example.com", Phone = "9876543210" },
                new Customer { Id = 2, FullName = "Sneha Rani", Email = "sneha@example.com", Phone = "9898989898" }
            );
        }
    }
}