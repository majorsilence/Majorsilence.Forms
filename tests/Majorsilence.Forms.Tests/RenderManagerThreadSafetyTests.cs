using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Renderers;
using Xunit;

namespace Majorsilence.Forms.Tests
{
    /// <summary>
    /// <c>RenderManager.SetRenderer</c> is public and the renderer table is read on every paint, so a change on one
    /// thread must never break a lookup on another.
    /// </summary>
    public class RenderManagerThreadSafetyTests
    {
        [Fact]
        public async Task Lookups_keep_working_while_renderers_are_replaced ()
        {
            var original = RenderManager.GetRenderer<Majorsilence.Forms.Renderers.ButtonRenderer> ();
            Assert.NotNull (original);

            var failures = new ConcurrentBag<string> ();
            using var stop = new CancellationTokenSource ();

            var readers = new Task[4];
            for (var i = 0; i < readers.Length; i++) {
                readers[i] = Task.Run (() => {
                    while (!stop.IsCancellationRequested) {
                        try {
                            if (RenderManager.GetRenderer<Majorsilence.Forms.Renderers.ButtonRenderer> () is null)
                                failures.Add ("a renderer disappeared");
                            if (RenderManager.GetRenderer<Renderer> (new Button ()) is null)
                                failures.Add ("no renderer for a Button");
                        } catch (System.Exception ex) {
                            failures.Add (ex.GetType ().Name + ": " + ex.Message);
                        }
                    }
                }, TestContext.Current.CancellationToken);
            }

            try {
                for (var n = 0; n < 2000; n++)
                    RenderManager.SetRenderer<Button> (n % 2 == 0 ? new Majorsilence.Forms.Renderers.ButtonRenderer () : original!);
            } finally {
                RenderManager.SetRenderer<Button> (original!);
                stop.Cancel ();
                await Task.WhenAll (readers);
            }

            Assert.Empty (failures);
        }
    }
}
