namespace ContestJudging.Services.Validation
{
    public record ValidationResult(bool IsValid, string ErrorMessage, int ComponentCount);
}
