using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Models;

namespace AdministratorWeb.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{ 
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) 
    {
    }
 
    public DbSet<LaundryRequest> LaundryRequests { get; set; }
    public DbSet<LaundrySettings> LaundrySettings { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<PaymentAdjustment> PaymentAdjustments { get; set; }
    public DbSet<BluetoothBeacon> BluetoothBeacons { get; set; }
    public DbSet<Room> Rooms { get; set; }
    public DbSet<RobotState> RobotStates { get; set; }
    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<LaundryRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.CustomerPhone).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Address).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Instructions).HasMaxLength(1000);
            entity.Property(e => e.DeclineReason).HasMaxLength(500);
            entity.Property(e => e.TotalCost).HasPrecision(10, 2);
            entity.Property(e => e.Weight).HasPrecision(8, 2);


            entity.HasOne(e => e.HandledBy)
                  .WithMany(u => u.HandledRequests)
                  .HasForeignKey(e => e.HandledById)
                  .OnDelete(DeleteBehavior.SetNull);
        });


        builder.Entity<LaundrySettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RatePerKg).HasPrecision(8, 2);
            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.CompanyAddress).HasMaxLength(500);
            entity.Property(e => e.CompanyPhone).HasMaxLength(20);
            entity.Property(e => e.OperatingHours).HasMaxLength(100);
            entity.Property(e => e.MaxWeightPerRequest).HasPrecision(8, 2);
            entity.Property(e => e.MinWeightPerRequest).HasPrecision(8, 2);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.FirstName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.LastName).IsRequired().HasMaxLength(50);
            entity.Property(e => e.RoomName).HasMaxLength(100);
            entity.Property(e => e.RoomDescription).HasMaxLength(200);
            entity.Property(e => e.AssignedBeaconMacAddress).HasMaxLength(17);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.TransactionId).HasMaxLength(50);
            entity.Property(e => e.PaymentReference).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.FailureReason).HasMaxLength(500);

            entity.HasOne(e => e.LaundryRequest)
                  .WithMany()
                  .HasForeignKey(e => e.LaundryRequestId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ProcessedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ProcessedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PaymentAdjustment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.Description).IsRequired().HasMaxLength(500);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.CreatedByUserId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CreatedByUserName).HasMaxLength(100);

            entity.HasIndex(e => e.EffectiveDate);
            entity.HasIndex(e => e.CreatedAt);
        });
        
        builder.Entity<BluetoothBeacon>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MacAddress).IsRequired().HasMaxLength(17);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.RoomName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreatedBy).HasMaxLength(450);
            entity.Property(e => e.UpdatedBy).HasMaxLength(450);
            entity.Property(e => e.LastSeenByRobot).HasMaxLength(100);
            
            // Create unique index on MAC address
            entity.HasIndex(e => e.MacAddress).IsUnique();
            
            // Create index on room name for faster queries
            entity.HasIndex(e => e.RoomName);
            
            // Create index on IsBase for quick base beacon lookup
            entity.HasIndex(e => e.IsBase);
        });
        
        builder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.CreatedBy).HasMaxLength(450);
            entity.Property(e => e.UpdatedBy).HasMaxLength(450);

            // Create unique index on room name
            entity.HasIndex(e => e.Name).IsUnique();

            // Create index on IsActive for faster filtering
            entity.HasIndex(e => e.IsActive);
        });

        builder.Entity<RobotState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.RobotName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.CurrentTask).HasMaxLength(500);
            entity.Property(e => e.CurrentLocation).HasMaxLength(200);
            entity.Property(e => e.LastKnownBeaconMac).HasMaxLength(100);
            entity.Property(e => e.LastKnownRoom).HasMaxLength(200);

            // Create unique index on robot name
            entity.HasIndex(e => e.RobotName).IsUnique();
        });

        builder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.CustomerName).HasMaxLength(100);
            entity.Property(e => e.SenderId).IsRequired().HasMaxLength(450);
            entity.Property(e => e.SenderName).HasMaxLength(100);
            entity.Property(e => e.SenderType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Content).IsRequired();
            entity.Property(e => e.ImageUrl).HasMaxLength(500);

            // Create index on CustomerId for faster conversation queries
            entity.HasIndex(e => e.CustomerId);

            // Create index on SentAt for ordering
            entity.HasIndex(e => e.SentAt);

            // Create index on IsRead for unread message queries
            entity.HasIndex(e => e.IsRead);

            // Composite index for customer conversations
            entity.HasIndex(e => new { e.CustomerId, e.SentAt });
        });

    }
}