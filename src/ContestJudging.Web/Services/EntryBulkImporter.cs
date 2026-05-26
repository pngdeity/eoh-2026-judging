using System;
using System.Collections.Generic;
using System.Linq;

namespace ContestJudging.Web.Services
{
    public static class EntryBulkImporter
    {
        public static List<string> ParseNewEntries(
            string bulkText,
            IReadOnlySet<string> existingEntryIds)
        {
            return bulkText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .Where(id => !existingEntryIds.Contains(id))
                .ToList();
        }
    }
}
