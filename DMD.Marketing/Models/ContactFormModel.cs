using System.ComponentModel.DataAnnotations;

namespace DMD.Marketing.Models;

public class ContactFormModel
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Company is required")]
    [StringLength(150)]
    public string Company { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "Please select your business type")]
    public string BusinessType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Message is required")]
    [StringLength(1000, ErrorMessage = "Message must be under 1000 characters")]
    public string Message { get; set; } = string.Empty;
}
