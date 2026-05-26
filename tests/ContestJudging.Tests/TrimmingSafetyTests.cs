using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using ContestJudging.Core.Entities;

using Xunit;

namespace ContestJudging.Tests
{
    /// <summary>
    /// Exercises code paths that are commonly broken by aggressive IL trimming.
    /// If these tests pass in a trimmed build, the optimization is likely safe.
    /// </summary>
    /// <remarks>
    /// Trimming safety strategy:
    /// 1. The web project disables reflection-based trimming analysis by default.
    /// 2. Core domain entities (Entry, Category) are intentionally preserved because
    ///    they must serialize/deserialize for localStorage and database operations.
    /// 3. Tests validate that JsonSerializer (reflection-based) and Activator.CreateInstance
    ///    work with domain types under a trimmed environment.
    /// 4. Infrastructure tests use [UnconditionalSuppressMessage] because EF Core
    ///    is not trimming-safe, so they are excluded from trimmed builds.
    /// 5. Individual methods annotate reflection usage with [RequiresUnreferencedCode]
    ///    so the trimmer can warn at compile time.
    /// </remarks>
    [Trait("Category", "Unit")]
    [Trait("Category", "Unit")]
    public class TrimmingSafetyTests
    {
        [Fact]
        [RequiresUnreferencedCode("Testing reflection-based JSON serialization.")]
        public void JsonSerialization_ShouldWork_WithDomainEntities()
        {
            // Reflection-based JSON serialization is the most common victim of trimming.
            // Even though we disabled reflection by default in the Web project, 
            // the Core logic must remain serializable.

            var cat = new Category("Test", 100);
            var entry = new Entry("E1");
            entry.SetScore(cat, 85.5);

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(entry, options);

            Assert.Contains("E1", json);
            Assert.Contains("85.5", json);

            var deserialized = JsonSerializer.Deserialize<Entry>(json);
            Assert.NotNull(deserialized);
            Assert.Equal("E1", deserialized.Id);
        }

        [Fact]
        public void EntityConstructor_ShouldBePreserved()
        {
            // Verifies that constructors used by repositories/factories aren't trimmed.
            var type = typeof(Entry);
            var instance = Activator.CreateInstance(type, new object[] { "DynamicEntry" });

            Assert.NotNull(instance);
            Assert.Equal("DynamicEntry", ((Entry)instance).Id);
        }
    }
}
