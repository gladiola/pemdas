using System.ComponentModel.DataAnnotations;

namespace pemdas.Models;

public sealed class PemdasPageViewModel
{
    [Required]
    [Display(Name = "Expression")]
    [RegularExpression(@"^[\d\s.+\-*/^()\[\]{}]+$",
        ErrorMessage = "Only numbers, arithmetic operators (+, -, *, /, ^), and grouping symbols ( ) [ ] {{ }} are allowed.")]
    public string Expression { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public string? FinalAnswer { get; set; }

    public IReadOnlyList<PemdasStepViewModel> Steps { get; init; } = [];
}

