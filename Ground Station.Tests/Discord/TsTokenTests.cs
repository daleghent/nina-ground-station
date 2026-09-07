using DaleGhent.NINA.GroundStation.Utilities;
using Xunit;
using GSUtilities = DaleGhent.NINA.GroundStation.Utilities.Utilities;

namespace DaleGhent.NINA.GroundStation.Tests.Discord {

    public sealed class TsTokenTests {

        [Fact]
        public void ResolveTokens_WithoutTargetScheduler_UsesPlaceholders() {
            var text = GSUtilities.ResolveTokens("$$TSPROJECTNAME$$ / $$TSTARGETNAME$$");

            Assert.Equal("---- / ----", text);
        }

        [Fact]
        public void ResolveTokens_LeavesOtherTokensAloneWhenTsTokensAreAbsent() {
            var text = GSUtilities.ResolveTokens("hello $$TARGET_NAME$$");

            Assert.Equal("hello ----", text);
        }

        [Fact]
        public void TryReadTsNames_DoesNotThrowOnOrdinaryObjects() {
            var found = GSUtilities.TryReadTsNames(new OrdinaryContainer { Name = "DSO" }, out var project, out var target);

            Assert.False(found);
            Assert.Null(project);
            Assert.Null(target);
        }

        [Fact]
        public void TryReadTsNames_ReadsProjectTargetDisplayWithBraces() {
            var source = new TsDisplayContainer {
                ProjectTargetDisplay = "{Galaxy} Shell Galaxy / NGC 474"
            };

            var found = GSUtilities.TryReadTsNames(source, out var project, out var target);

            Assert.True(found);
            Assert.Equal("{Galaxy} Shell Galaxy", project);
            Assert.Equal("NGC 474", target);
        }

        [Fact]
        public void TryReadTsNames_ReadsPrivatePlanField() {
            var source = new PlanContainerStub(new FakePlan {
                PlanTarget = new FakePlanTarget {
                    Name = "NGC 474",
                    Project = new FakeProject { Name = "{Galaxy} Shell Galaxy" }
                }
            });

            var found = GSUtilities.TryReadTsNames(source, out var project, out var target);

            Assert.True(found);
            Assert.Equal("{Galaxy} Shell Galaxy", project);
            Assert.Equal("NGC 474", target);
        }

        [Fact]
        public void TrySplitProjectTargetDisplay_RejectsEmptyAndMalformed() {
            Assert.False(GSUtilities.TrySplitProjectTargetDisplay(null, out _, out _));
            Assert.False(GSUtilities.TrySplitProjectTargetDisplay(string.Empty, out _, out _));
            Assert.False(GSUtilities.TrySplitProjectTargetDisplay("no-separator", out _, out _));
            Assert.False(GSUtilities.TrySplitProjectTargetDisplay(" / NGC 474", out _, out _));
        }

        private sealed class OrdinaryContainer {
            public string Name { get; set; }
        }

        private sealed class TsDisplayContainer {
            public string ProjectTargetDisplay { get; set; }
        }

        private sealed class PlanContainerStub {
            private readonly object plan;

            public PlanContainerStub(object plan) {
                this.plan = plan;
            }
        }

        private sealed class FakePlan {
            public FakePlanTarget PlanTarget { get; set; }
        }

        private sealed class FakePlanTarget {
            public string Name { get; set; }
            public FakeProject Project { get; set; }
        }

        private sealed class FakeProject {
            public string Name { get; set; }
        }
    }
}
