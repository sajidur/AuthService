
using AuthMicroservice.Controller;
using System.ComponentModel.DataAnnotations;
namespace AuthMicroservice.Model
{


    public class AtLeastOneRequiredAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is not UserName userName)
            {
                return new ValidationResult("UserName is required.");
            }

            bool hasEmail = !string.IsNullOrWhiteSpace(userName.Email);
            bool hasMobile = !string.IsNullOrWhiteSpace(userName.MobileNumber);
            bool hasFirst = !string.IsNullOrWhiteSpace(userName.FirstName);
            bool hasLast = !string.IsNullOrWhiteSpace(userName.LastName);

            if (hasEmail || hasMobile || (hasFirst && hasLast))
            {
                return ValidationResult.Success;
            }

            return new ValidationResult(
                "Provide either Email, MobileNumber, or both FirstName and LastName."
            );
        }
    }
}
