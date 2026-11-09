using System.ComponentModel.DataAnnotations;

namespace RhuHealthRecords.Models
{
    public class PatientRegistrationViewModel
    {
        [Display(Name = "Patient ID")]
        public string? PatientId { get; set; }

        [Display(Name = "PhilHealth No.")]
        [RegularExpression(@"^\d{2}-\d{9}-\d{1}$", ErrorMessage = "Use the format XX-XXXXXXXXX-X.")]
        public string? PhilHealthNo { get; set; }

        [Required(ErrorMessage = "First name is required.")]
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Display(Name = "Middle Name")]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required.")]
        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [Display(Name = "Suffix")]
        public string? Suffix { get; set; }

        [Required(ErrorMessage = "Date of birth is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Sex is required.")]
        [Display(Name = "Sex")]
        public string? Sex { get; set; }

        [Display(Name = "Civil Status")]
        public string? CivilStatus { get; set; } = "Single";

        [Display(Name = "Occupation")]
        public string? Occupation { get; set; }
    }
}
