using DaleGhent.NINA.GroundStation.Utilities;
using System;
using System.Globalization;
using Xunit;
using GSUtilities = DaleGhent.NINA.GroundStation.Utilities.Utilities;

namespace DaleGhent.NINA.GroundStation.Tests.Discord {

    public sealed class SessionTokenTests {
        [Theory]
        [InlineData("2026-08-23T15:59:59", "2026-08-22")]
        [InlineData("2026-08-23T16:00:00", "2026-08-23")]
        [InlineData("2026-08-23T16:00:01", "2026-08-23")]
        public void SessionDateTime_RollsAtSixteenHundred(string localTime, string expectedDate) {
            var now = DateTime.Parse(localTime, CultureInfo.InvariantCulture, DateTimeStyles.None);
            var session = GSUtilities.SessionDateTime(now, TimeSpan.FromHours(16));

            Assert.Equal(DateTime.Parse(expectedDate, CultureInfo.InvariantCulture).Date, session.Date);
            Assert.Equal(now.TimeOfDay, session.TimeOfDay);
        }

        [Fact]
        public void SessionDateTime_HonorsCustomRollover() {
            var now = DateTime.Parse("2026-08-23T11:59:00", CultureInfo.InvariantCulture);
            var before = GSUtilities.SessionDateTime(now, TimeSpan.FromHours(12));
            var after = GSUtilities.SessionDateTime(now.AddMinutes(1), TimeSpan.FromHours(12));

            Assert.Equal(new DateTime(2026, 8, 22).Date, before.Date);
            Assert.Equal(new DateTime(2026, 8, 23).Date, after.Date);
        }

        [Fact]
        public void ResolveTokens_SessionDateTokensUseRollover() {
            var previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            try {
                var beforeRollover = DateTime.Parse("2026-08-23T15:59:00", CultureInfo.InvariantCulture);
                var afterRollover = DateTime.Parse("2026-08-23T16:00:00", CultureInfo.InvariantCulture);

                var formattedBefore = GSUtilities.ResolveTokens("$$FORMAT_SESSIONDATETIME yyyy-MM-dd$$", nowOverride: beforeRollover);
                var formattedAfter = GSUtilities.ResolveTokens("$$FORMAT_SESSIONDATETIME yyyy-MM-dd$$", nowOverride: afterRollover);

                Assert.Equal("2026-08-22", formattedBefore);
                Assert.Equal("2026-08-23", formattedAfter);
            } finally {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void ResolveTokens_InvalidSessionFormatIsReplaced() {
            var text = GSUtilities.ResolveTokens("$$FORMAT_SESSIONDATETIME %$$", nowOverride: new DateTime(2026, 8, 23, 20, 0, 0));
            Assert.Equal("[Invalid DateTime format]", text);
        }

        [Fact]
        public void ResolveTokens_LiteralThreadNameIsUnchanged() {
            Assert.Equal("test-session", GSUtilities.ResolveTokens("test-session", nowOverride: DateTime.Now));
        }

        [Fact]
        public void ResolveFailureTokens_FillsFailedItemFields() {
            var failed = new FailedItem {
                Name = "Slew",
                Description = "GoTo target",
                Category = "Telescope",
                ParentName = "NGC 7331",
                Attempts = 3,
                Reasons = { new FailureReason { Reason = "timeout" }, new FailureReason { Reason = "unsafe" } },
            };

            var text = GSUtilities.ResolveFailureTokens(
                "$$FAILED_ITEM$$|$$FAILED_ITEM_DESC$$|$$FAILED_ITEM_CATEGORY$$|$$FAILED_ATTEMPTS$$|$$FAILED_INSTR_SET$$|$$ERROR_LIST$$",
                failed);

            Assert.Equal("Slew|GoTo target|Telescope|3|NGC 7331|timeout, unsafe", text);
        }

        [Fact]
        public void ResolveFailureTokens_UsesPlaceholdersWhenParentAndReasonsAreMissing() {
            var failed = new FailedItem { Name = "Wait" };
            var text = GSUtilities.ResolveFailureTokens("$$FAILED_INSTR_SET$$|$$ERROR_LIST$$", failed);
            Assert.Equal("----|", text);
        }
    }
}
