﻿using System.ComponentModel.DataAnnotations;

namespace SmE_CommerceModels.RequestDtos.User;

public class CreateUserReqDto
{
    [Required]
    [EmailAddress(ErrorMessage = "Must be email format")]
    public string Email { get; set; } = null!;

    [Required]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*()_+~\-={}[\]:;""'<>?,.\/]).{8,}$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one digit, one special character, and must be at least 8 characters long."
    )]
    public string Password { get; set; } = null!;

    [Required]
    public string FullName { get; set; } = null!;

    [Required]
    public string Role { get; set; } = null!;
}
