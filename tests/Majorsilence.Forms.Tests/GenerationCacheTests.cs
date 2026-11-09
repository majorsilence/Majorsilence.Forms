using System.Threading;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    // The cache TextMeasurer keeps its blocks and sizes in stays bounded while threads add to it at once. Only the thread
    // whose add filled a generation started the next one, so while that thread was held up between the two (preempted,
    // or a virtual CPU not scheduled), every other thread kept adding to the full generation: it grew for as long as
    // the hold-up lasted, not by "an entry or two". A loaded CI runner held 2574 entries against a bound of 2008. Now
    // any add past the limit starts the next generation, and a late swap by the held-up thread cannot replace the
    // generations started meanwhile.
    //
    // These tests use their own cache, so nothing else in the process that measures text can change the counts.
    public class GenerationCacheTests
    {
        private const int Size = 100;

        [Fact]
        public void Adds_while_the_filling_thread_is_held_up_start_the_next_generation ()
        {
            // The hook runs inside the add that filled the first generation, before that add starts the next one: the
            // adds it makes are what another thread does while the filling one is held up there.
            GenerationCache<string, int>? cache = null;
            var held = false;
            cache = new GenerationCache<string, int> (Size, () => {
                if (held)
                    return;
                held = true;
                for (var i = 0; i < Size * 3 / 2; i++)
                    cache!.Add ($"meanwhile {i}", i);
            });

            for (var i = 0; i < Size; i++)
                cache.Add ($"first {i}", i);

            Assert.True (held);
            Assert.InRange (cache.Count, 1, 2 * Size + 1);

            // The held-up add's swap came last; it must not have put back the full generation in place of the newer ones.
            Assert.True (cache.TryGet ($"meanwhile {Size * 3 / 2 - 1}", out var last));
            Assert.Equal (Size * 3 / 2 - 1, last);
        }

        [Fact]
        public void Threads_adding_while_the_filling_thread_is_held_up_keep_the_cache_bounded ()
        {
            const int threads = 8;
            const int perThread = 4 * Size;
            var finished = 0;
            var holds = 0;
            var othersFinishedWhileHeld = false;
            var countWhileHeld = 0;
            GenerationCache<string, int>? cache = null;

            cache = new GenerationCache<string, int> (Size, () => {
                // Hold up the first thread to fill a generation until every other thread has added all its entries, and
                // count then: once released, that thread's own adds swap the overgrown generation out again.
                if (Interlocked.Increment (ref holds) == 1) {
                    othersFinishedWhileHeld = SpinWait.SpinUntil (() => Volatile.Read (ref finished) == threads - 1, 10_000);
                    countWhileHeld = cache!.Count;
                }
            });

            var workers = new Thread[threads];
            for (var t = 0; t < threads; t++) {
                var thread = t;
                workers[t] = new Thread (() => {
                    for (var i = 0; i < perThread; i++)
                        cache.Add ($"{thread} {i}", i);
                    Interlocked.Increment (ref finished);
                });
                workers[t].Start ();
            }

            foreach (var worker in workers)
                worker.Join ();

            Assert.True (othersFinishedWhileHeld);
            Assert.InRange (countWhileHeld, 1, 2 * Size + threads);
            Assert.InRange (cache.Count, 1, 2 * Size + threads);
        }
    }
}
