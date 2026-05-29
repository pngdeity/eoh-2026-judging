using System.Collections.Generic;
using System.Linq;

using ContestJudging.Core.Entities;
using ContestJudging.Web.Pages;

namespace ContestJudging.Web.Services
{
    public static class LeaderboardBuilder
    {
        public static List<Results.LeaderboardItem> Build(IReadOnlyList<Entry> entries)
        {
            var items = entries
                .Select(e => new Results.LeaderboardItem { Entry = e })
                .OrderByDescending(i => i.Entry.TotalScore)
                .ToList();

            for (int i = 0; i < items.Count; i++)
            {
                items[i].Rank = i + 1;
            }

            return items;
        }
    }
}
