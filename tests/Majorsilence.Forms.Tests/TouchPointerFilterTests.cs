using Xunit;

namespace Majorsilence.Forms.Tests
{
    public class TouchPointerFilterTests
    {
        [Fact]
        public void ASecondFingerWhileTheFirstIsDown_IsIgnored_ThroughItsWholeLife ()
        {
            var filter = new TouchPointerFilter ();

            Assert.True (filter.Pressed (1, isTouch: true));
            Assert.False (filter.Pressed (2, isTouch: true));
            Assert.False (filter.Moved (2, isTouch: true));
            Assert.True (filter.Moved (1, isTouch: true));
            Assert.False (filter.Released (2, isTouch: true));      // the second finger lifting must not release the first control
            Assert.True (filter.Released (1, isTouch: true));
        }

        [Fact]
        public void AfterTheFirstFingerLifts_TheNextOneIsFollowed ()
        {
            var filter = new TouchPointerFilter ();
            filter.Pressed (1, isTouch: true);
            filter.Pressed (2, isTouch: true);
            filter.Released (1, isTouch: true);

            Assert.True (filter.Pressed (3, isTouch: true));
            Assert.True (filter.Released (3, isTouch: true));
        }

        [Fact]
        public void TheMouse_IsNeverFiltered ()
        {
            var filter = new TouchPointerFilter ();
            filter.Pressed (1, isTouch: true);

            Assert.True (filter.Pressed (99, isTouch: false));
            Assert.True (filter.Moved (99, isTouch: false));
            Assert.True (filter.Released (99, isTouch: false));
            Assert.False (filter.Pressed (2, isTouch: true));       // and the finger is still the one being followed
        }

        [Fact]
        public void AFingerThatWentSilent_IsReplacedByTheNextPress ()
        {
            var time = TimeSpan.Zero;
            var filter = new TouchPointerFilter (() => time);
            filter.Pressed (1, isTouch: true);

            time += TimeSpan.FromSeconds (2);
            Assert.False (filter.Pressed (2, isTouch: true));       // still plausibly held

            time += TouchPointerFilter.StaleAfter;
            Assert.True (filter.Pressed (2, isTouch: true));        // its release was lost: input is not locked to it for ever
            Assert.False (filter.Moved (1, isTouch: true));
        }

        [Fact]
        public void AMoveOrReleaseWithNoPress_IsFollowed ()
        {
            var filter = new TouchPointerFilter ();

            Assert.True (filter.Moved (5, isTouch: true));
            Assert.True (filter.Released (5, isTouch: true));
        }
    }
}
