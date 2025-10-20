using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models.DTOs
{
    public class CreateRequestDto
    {
        [Required]
        public string CustomerId { get; set; } = string.Empty;
        
        [Required]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        [Phone]
        public string CustomerPhone { get; set; } = string.Empty;
        
        [Required]
        public string Address { get; set; } = string.Empty;
        
        public string? Instructions { get; set; }
        
        [Required]
        public RequestType Type { get; set; }
        
        public DateTime? ScheduledAt { get; set; }
    }
}