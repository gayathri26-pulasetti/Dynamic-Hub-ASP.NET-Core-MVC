namespace MVCDHProject.Models;
using System.ComponentModel.DataAnnotations;

    public class RegisterViewModel
    {
        [Required]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Mobile { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

