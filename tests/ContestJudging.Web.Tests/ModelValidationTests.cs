using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using ContestJudging.Web.Pages;

using Xunit;

namespace ContestJudging.Web.Tests;

[Trait("Category", "Unit")]
[Trait("Category", "Unit")]
public class ModelValidationTests
{
    [Fact]
    [RequiresUnreferencedCode("DataAnnotations validation uses reflection which is not trim-safe")]
    public void CategoryModel_ValidValues_SucceedsValidation()
    {
        var model = new Setup.CategoryModel { Id = "cat1", MaxScore = 10 };
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    [RequiresUnreferencedCode("DataAnnotations validation uses reflection which is not trim-safe")]
    public void CategoryModel_EmptyId_FailsValidation()
    {
        var model = new Setup.CategoryModel { Id = "", MaxScore = 10 };
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Setup.CategoryModel.Id)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-5)]
    [RequiresUnreferencedCode("DataAnnotations validation uses reflection which is not trim-safe")]
    public void CategoryModel_MaxScoreBelowMinimum_FailsValidation(double maxScore)
    {
        var model = new Setup.CategoryModel { Id = "cat1", MaxScore = maxScore };
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Setup.CategoryModel.MaxScore)));
    }

    [Fact]
    [RequiresUnreferencedCode("DataAnnotations validation uses reflection which is not trim-safe")]
    public void EntryModel_ValidValues_SucceedsValidation()
    {
        var model = new Setup.EntryModel { Id = "entry1" };
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, true);

        Assert.True(isValid);
        Assert.Empty(results);
    }

    [Fact]
    [RequiresUnreferencedCode("DataAnnotations validation uses reflection which is not trim-safe")]
    public void EntryModel_EmptyId_FailsValidation()
    {
        var model = new Setup.EntryModel { Id = "" };
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, new ValidationContext(model), results, true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(Setup.EntryModel.Id)));
    }

    [Fact]
    public void LeaderboardItem_StoresEntryCorrectly()
    {
        var entry = new ContestJudging.Core.Entities.Entry("E1");
        var item = new Results.LeaderboardItem { Entry = entry };

        Assert.Same(entry, item.Entry);
    }
}
