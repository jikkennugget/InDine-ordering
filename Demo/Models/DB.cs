using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Demo.Models;

#nullable disable warnings

public class DB : DbContext
{
    public DB(DbContextOptions options) : base(options) { }

    // DB Sets
    public DbSet<User> Users { get; set; }
    public DbSet<Admin> Admins { get; set; }
    public DbSet<Staff> Staff { get; set; }
    public DbSet<ProductCategory> ProductCategories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderLine> OrderLines { get; set; }
}

// Entity Classes -------------------------------------------------------------

public class User
{
    [Key, MaxLength(100)]
    public string Email { get; set; }
    [MaxLength(100)]
    public string Hash { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }
    
    // Password reset fields
    [MaxLength(100)]
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }

    public string Role => GetType().Name;
}

public class Admin : User
{

}

public class Staff : User
{
    [MaxLength(100)]
    public string PhotoURL { get; set; }
}

// Product, Order, OrderLine

public class ProductCategory
{

    [Key, MaxLength(4)]
    public string Id { get; set; }
    [MaxLength(50)]
    public string Name { get; set; }
}

public class Product
{
    [Key, MaxLength(4)]
    public string Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; }
    [Precision(6, 2)]
    public decimal Price { get; set; }
    [MaxLength(100)]
    public string? PhotoURL { get; set; }

    // Foreign Keys
    [MaxLength(4)]
    [Required]
    public string CategoryId { get; set; }

    // Navigation Properties
    public ProductCategory? Category { get; set; }
    public List<OrderLine> Lines { get; set; } = [];
}

public class Order
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public bool Paid { get; set; }
    [MaxLength(10)]
    public string TableNumber { get; set; }
    [MaxLength(20)]
    public string PaymentMethod { get; set; }

    // Navigation Properties
    public List<OrderLine> OrderLines { get; set; } = [];
}

public class OrderLine
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    [Precision(6, 2)]
    public decimal Price { get; set; }
    public int Quantity { get; set; }

    // Foreign Keys
    public int OrderId { get; set; }
    public string ProductId { get; set; }

    // Navigation Properties
    public Order Order { get; set; }
    public Product Product { get; set; }
}
