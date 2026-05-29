// Requires: dotnet run --urls "http://localhost:5000" running in background
// CI: Playwright browsers installed via pwsh, app started with dotnet run &, 30s warmup

using System;
using System.Threading.Tasks;

using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

using NUnit.Framework;

namespace ContestJudging.E2ETests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
[Category("E2E")]
public class AppE2ETests : PageTest
{
    private const string AppUrl = "http://localhost:5000";
    private const int DefaultTimeout = 60000;

    [SetUp]
    public async Task Setup()
    {
        Page.Console += (_, e) => { if (e.Type == "error") TestContext.Progress.WriteLine($"[browser error] {e.Text}"); };
        Page.PageError += (_, e) => TestContext.Progress.WriteLine($"[page error] {e}");
        await Page.GotoAsync($"{AppUrl}/");
        await WaitForBlazorAsync();
        await Expect(Page.Locator("text=Forging the Future")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });
    }

    private async Task NavigateToAsync(string path)
    {
        var href = path.TrimStart('/');
        if (string.IsNullOrEmpty(href))
        {
            var homeLink = Page.Locator("a.nav-link[href=\"\"]");
            await homeLink.ClickAsync(new() { Force = true });
        }
        else
        {
            var navLink = Page.Locator($"a.nav-link[href=\"{href}\"]");
            await navLink.ClickAsync(new() { Force = true });
        }
        await Page.WaitForTimeoutAsync(300);
    }

    private async Task WaitForBlazorAsync()
    {
        await Page.EvaluateAsync(@"() => new Promise(resolve => {
            if (typeof window.Blazor !== 'undefined') { resolve(); return; }
            const interval = setInterval(() => {
                if (typeof window.Blazor !== 'undefined') {
                    clearInterval(interval);
                    resolve();
                }
            }, 100);
        })");
        await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    }

    [Test]
    public async Task FullJudgingWorkflow_SetupToScoring()
    {
        await NavigateToAsync("/setup");
        await Expect(Page.Locator("h3:has-text('Contest Setup')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        var categoryCard = Page.Locator(".card").Nth(0);
        await categoryCard.Locator("input").First.FillAsync("Innovation");
        await categoryCard.Locator("input").Nth(1).FillAsync("10");
        await categoryCard.Locator("button[type='submit']").ClickAsync();
        await Expect(Page.Locator("text=Innovation (Max: 10)")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        var entryCard = Page.Locator(".card").Nth(1);
        foreach (var entry in new[] { "ProjectA", "ProjectB", "ProjectC" })
        {
            await entryCard.Locator("input").First.FillAsync(entry);
            await entryCard.Locator("button[type='submit']").ClickAsync();
        }
        await Expect(Page.Locator("text=ProjectC")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await NavigateToAsync("/judging");
        await Expect(Page.Locator("h3:has-text('Judging')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await WaitForSelectAndPick("Innovation");
        await Expect(Page.Locator(".judge-card").First).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await Page.Locator(".judge-card").First.ClickAsync();
        await Page.Locator(".judge-card").First.ClickAsync();
        await Expect(Page.Locator("h5:has-text('Recorded Relations')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await NavigateToAsync("/results");
        await Expect(Page.Locator("h3:has-text('Results')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await Page.Locator("button:has-text('Calculate Results')").ClickAsync();
        await Expect(Page.Locator("table.table-hover")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await Expect(Page.Locator("text=ProjectA")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });
        await Expect(Page.Locator("text=ProjectB")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });
        await Expect(Page.Locator("text=ProjectC")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });
    }

    [Test]
    public async Task DataPersists_AcrossPageNavigations()
    {
        await NavigateToAsync("/setup");
        await Expect(Page.Locator("h3:has-text('Contest Setup')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        var categoryCard = Page.Locator(".card").Nth(0);
        await categoryCard.Locator("input").First.FillAsync("TestCat");
        await categoryCard.Locator("input").Nth(1).FillAsync("10");
        await categoryCard.Locator("button[type='submit']").ClickAsync();
        await Expect(Page.Locator("text=TestCat (Max: 10)")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        var entryCard = Page.Locator(".card").Nth(1);
        await entryCard.Locator("input").First.FillAsync("Entry1");
        await entryCard.Locator("button[type='submit']").ClickAsync();
        await entryCard.Locator("input").First.FillAsync("Entry2");
        await entryCard.Locator("button[type='submit']").ClickAsync();
        await Expect(Page.Locator("text=Entry1")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });
        await Expect(Page.Locator("text=Entry2")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await NavigateToAsync("/judging");
        await Expect(Page.Locator("h3:has-text('Judging')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await WaitForSelectAndPick("TestCat");
        await Expect(Page.Locator(".judge-card").First).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await NavigateToAsync("/setup");
        await Expect(Page.Locator("h3:has-text('Contest Setup')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });
        await Expect(Page.Locator("text=TestCat (Max: 10)")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });
        await Expect(Page.Locator("text=Entry1")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });
        await Expect(Page.Locator("text=Entry2")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });
    }

    [Test]
    public async Task PartitionPlanning_CreatesGroups()
    {
        await NavigateToAsync("/setup");
        await Expect(Page.Locator("h3:has-text('Contest Setup')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await Page.Locator("textarea").FillAsync("E1\nE2\nE3\nE4\nE5\nE6\nE7\nE8\nE9\nE10");
        await Page.Locator("button:has-text('Import Entries')").ClickAsync();
        await Expect(Page.Locator("text=E10")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        var partitionCard = Page.Locator(".card:has(h4:has-text('Partition Planning'))");
        await partitionCard.ScrollIntoViewIfNeededAsync();
        await partitionCard.Locator("input[type='number']").First.FillAsync("2");
        await partitionCard.Locator("input[type='number']").Nth(1).FillAsync("0.1");
        await partitionCard.Locator("button:has-text('Preview Partitions')").ClickAsync();
        await Expect(Page.Locator("text=Group 0")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });
        await Expect(Page.Locator("text=Group 1")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        var bridgeIcons = Page.Locator("i.bi-link-45deg");
        var count = await bridgeIcons.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Expected at least one bridge node with link icon");
    }

    [Test]
    public async Task GlobalRankings_RespectsTransitiveOrder()
    {
        await NavigateToAsync("/setup");
        await Expect(Page.Locator("h3:has-text('Contest Setup')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        var categoryCard = Page.Locator(".card").Nth(0);
        await categoryCard.Locator("input").First.FillAsync("Ranking");
        await categoryCard.Locator("input").Nth(1).FillAsync("50");
        await categoryCard.Locator("button[type='submit']").ClickAsync();
        await Expect(Page.Locator("text=Ranking (Max: 50)")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        var entryCard = Page.Locator(".card").Nth(1);
        foreach (var entry in new[] { "Gold", "Silver", "Bronze" })
        {
            await entryCard.Locator("input").First.FillAsync(entry);
            await entryCard.Locator("button[type='submit']").ClickAsync();
        }
        await Expect(Page.Locator("text=Bronze")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await NavigateToAsync("/judging");
        await Expect(Page.Locator("h3:has-text('Judging')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await WaitForSelectAndPick("Ranking");
        await Expect(Page.Locator("button:has-text('Manual Override / Correction')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await Page.Locator("button:has-text('Manual Override / Correction')").ClickAsync();
        await Expect(Page.Locator(".card-body:has(label:has-text('Exhibit A'))")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        var overrideBody = Page.Locator(".card-body:has(label:has-text('Exhibit A'))");
        var selects = overrideBody.Locator("select");
        var submitBtn = overrideBody.Locator("button:has-text('Submit')");

        await selects.Nth(0).SelectOptionAsync(new[] { "Gold" });
        await selects.Nth(1).SelectOptionAsync(new[] { "GreaterThan" });
        await selects.Nth(2).SelectOptionAsync(new[] { "Silver" });
        await submitBtn.ClickAsync();

        await selects.Nth(0).SelectOptionAsync(new[] { "Gold" });
        await selects.Nth(1).SelectOptionAsync(new[] { "GreaterThan" });
        await selects.Nth(2).SelectOptionAsync(new[] { "Bronze" });
        await submitBtn.ClickAsync();

        await selects.Nth(0).SelectOptionAsync(new[] { "Silver" });
        await selects.Nth(1).SelectOptionAsync(new[] { "GreaterThan" });
        await selects.Nth(2).SelectOptionAsync(new[] { "Bronze" });
        await submitBtn.ClickAsync();

        await NavigateToAsync("/results");
        await Expect(Page.Locator("h3:has-text('Results')")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        await Page.Locator("button:has-text('Calculate Results')").ClickAsync();
        await Expect(Page.Locator("table.table-hover")).ToBeVisibleAsync(new() { Timeout = DefaultTimeout });

        var rows = Page.Locator("table.table-hover tbody tr");
        await Expect(rows.Nth(0)).ToContainTextAsync("Gold");
        await Expect(rows.Nth(0)).ToContainTextAsync("#1");
        await Expect(rows.Nth(1)).ToContainTextAsync("Silver");
        await Expect(rows.Nth(1)).ToContainTextAsync("#2");
        await Expect(rows.Nth(2)).ToContainTextAsync("Bronze");
        await Expect(rows.Nth(2)).ToContainTextAsync("#3");
    }

    private async Task WaitForSelectAndPick(string optionLabel)
    {
        var select = Page.Locator("select.form-select").First;
        await select.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = DefaultTimeout });
        var option = select.Locator($"option[value='{optionLabel}']");
        await option.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = DefaultTimeout });
        await select.SelectOptionAsync(new[] { optionLabel }, new LocatorSelectOptionOptions { Timeout = DefaultTimeout });
    }
}
