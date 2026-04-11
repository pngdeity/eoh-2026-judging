using System;
using System.Collections.Generic;
using System.Text.Json;
using ContestJudging.Core.Entities;
using Xunit;

namespace ContestJudging.Tests
{
    /// <summary>
    /// Exercises code paths that are commonly broken by aggressive IL trimming.
    /// If these tests pass in a trimmed build, the optimization is likely safe.
    /// </summary>
    public class TrimmingSafetyTests
    {
        [Fact]
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
