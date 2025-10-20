using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Models;

namespace AdministratorWeb.Data
{
    public class DbSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<DbSeeder> _logger;

        public DbSeeder(ApplicationDbContext context, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ILogger<DbSeeder> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            _logger.LogInformation("Starting database seeding process...");
            
            try
            {
                await _context.Database.MigrateAsync();
                _logger.LogInformation("Database connection verified successfully");

                await SeedRolesAsync();
                await SeedUsersAsync();
                await SeedSettingsAsync();
                
                _logger.LogInformation("Database seeding completed successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during database seeding");
                throw;
            }
        }

        private async Task SeedRolesAsync()
        {
            _logger.LogInformation("Seeding roles...");
            var roles = new[] { "Administrator", "Member" };
            var createdRoles = 0;

            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    var result = await _roleManager.CreateAsync(new IdentityRole(role));
                    if (result.Succeeded)
                    {
                        createdRoles++;
                        _logger.LogInformation("Successfully created role: {Role}", role);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to create role {Role}: {Errors}", role, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    _logger.LogInformation("Role {Role} already exists, skipping...", role);
                }
            }
            
            _logger.LogInformation("Role seeding completed. Created {CreatedCount} new roles out of {TotalCount} total roles", createdRoles, roles.Length);
        }

        private async Task SeedUsersAsync()
        {
            _logger.LogInformation("Seeding users...");
            var users = new[]
            {
                new { Email = "admin1@laundry.com", FirstName = "Admin", LastName = "One", Role = "Administrator" },
                new { Email = "admin2@laundry.com", FirstName = "Admin", LastName = "Two", Role = "Administrator" },
                new { Email = "member1@laundry.com", FirstName = "Member", LastName = "One", Role = "Member" },
                new { Email = "member2@laundry.com", FirstName = "Member", LastName = "Two", Role = "Member" }
            };

            var createdUsers = 0;
            const string defaultPassword = "123"; // Very simple password due to lax requirements

            foreach (var userData in users)
            {
                var existingUser = await _userManager.FindByEmailAsync(userData.Email);
                if (existingUser == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = userData.Email,
                        Email = userData.Email,
                        FirstName = userData.FirstName,
                        LastName = userData.LastName,
                        EmailConfirmed = true,
                        IsActive = true
                    };

                    var result = await _userManager.CreateAsync(user, defaultPassword);
                    if (result.Succeeded)
                    {
                        var roleResult = await _userManager.AddToRoleAsync(user, userData.Role);
                        if (roleResult.Succeeded)
                        {
                            createdUsers++;
                            _logger.LogInformation("Successfully created user: {Email} with role: {Role}", userData.Email, userData.Role);
                        }
                        else
                        {
                            _logger.LogWarning("Created user {Email} but failed to assign role {Role}: {Errors}", 
                                userData.Email, userData.Role, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to create user {Email}: {Errors}", 
                            userData.Email, string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    _logger.LogInformation("User {Email} already exists, skipping...", userData.Email);
                }
            }
            
            _logger.LogInformation("User seeding completed. Created {CreatedCount} new users out of {TotalCount} total users", createdUsers, users.Length);
        }

        private async Task SeedSettingsAsync()
        {
            _logger.LogInformation("Seeding laundry settings...");
            
            if (!_context.LaundrySettings.Any())
            {
                var settings = new LaundrySettings
                {
                    RatePerKg = 15.00m,
                    CompanyName = "Autonomous Laundry Service",
                    CompanyAddress = "123 Tech Street, Innovation City",
                    CompanyPhone = "+1-555-LAUNDRY",
                    OperatingHours = "8:00 AM - 6:00 PM",
                    MaxWeightPerRequest = 50.0m,
                    MinWeightPerRequest = 1.0m
                };

                _context.LaundrySettings.Add(settings);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Successfully created laundry settings with rate: ${Rate}/kg", settings.RatePerKg);
            }
            else
            {
                var existingSettings = await _context.LaundrySettings.FirstAsync();
                _logger.LogInformation("Laundry settings already exist with rate: ${Rate}/kg, skipping...", existingSettings.RatePerKg);
            }
        }
    }
}