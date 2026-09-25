using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

#pragma warning disable CA1416

namespace Majorsilence.Forms
{
    // SMP-20 and SMP-21: loading an image, and finding out that it failed.
    //
    // Load (url) assigned ImageLocation, whose setter ran an `async void` that returned before a single
    // byte was read. So `pb.Load (path); var w = pb.Image.Width;` threw NullReferenceException, and
    // `try { pb.Load (path); } catch (FileNotFoundException) { }` never caught -- the exception surfaced
    // on a thread-pool thread inside an async void, where on some paths it tears the process down. A
    // missing LOCAL file was worse still: SKBitmap.Decode (path) returns null rather than throwing, so
    // the box reported no error and silently painted nothing.
    //
    // Upstream's Load is synchronous -- open, decode, install, return -- and rethrows outside design
    // mode (PictureBox.cs:457-500). This file makes ours do the same, and gives LoadAsync/CancelAsync
    // and the two async events the real implementations their `add { } remove { }` stubs discarded.
    public partial class PictureBox
    {
        private CancellationTokenSource? async_load;

        /// <summary>
        /// Occurs when an asynchronous image load started by <see cref="LoadAsync(string)"/> finishes,
        /// whether it succeeded, failed or was cancelled -- <see cref="AsyncCompletedEventArgs.Error"/>
        /// and <see cref="AsyncCompletedEventArgs.Cancelled"/> say which.
        /// </summary>
        public event AsyncCompletedEventHandler? LoadCompleted;

        /// <summary>Occurs as an asynchronous image load progresses.</summary>
        public event ProgressChangedEventHandler? LoadProgressChanged;

        /// <summary>
        /// Loads the image at the specified path or URL, synchronously, and sets
        /// <see cref="ImageLocation"/> to it.
        /// </summary>
        /// <param name="url">A local file path or an absolute URL.</param>
        /// <exception cref="InvalidOperationException"><paramref name="url"/> is null or blank.</exception>
        /// <remarks>
        /// The image is readable from <see cref="Image"/> the moment this returns, and a failure is
        /// raised rather than recorded: a load that cannot produce an image throws, having set
        /// <see cref="IsErrored"/>.
        /// </remarks>
        /// <remarks>
        /// Synchronous while <see cref="WaitOnLoad"/> is set, which is this layer's default; with it
        /// cleared the load runs in the background and the call returns at once, as upstream's does
        /// (W6 mechanisms). The default differs from upstream's deliberately -- see
        /// <see cref="WaitOnLoad"/>.
        /// </remarks>
        public void Load (string url)
        {
            if (string.IsNullOrWhiteSpace (url))
                throw new InvalidOperationException ("ImageLocation not specified.");

            if (!WaitOnLoad) {
                LoadAsync (url);
                return;
            }

            LoadCore (url, rethrow: true);
        }

        /// <summary>
        /// Starts loading the image at the specified path or URL in the background, raising
        /// <see cref="LoadProgressChanged"/> as it goes and <see cref="LoadCompleted"/> at the end.
        /// </summary>
        /// <param name="url">A local file path or an absolute URL.</param>
        /// <exception cref="InvalidOperationException"><paramref name="url"/> is null or blank.</exception>
        public void LoadAsync (string url)
        {
            if (string.IsNullOrWhiteSpace (url))
                throw new InvalidOperationException ("ImageLocation not specified.");

            // Upstream allows one load at a time: a second LoadAsync abandons the first.
            CancelAsync ();

            var cancellation = new CancellationTokenSource ();
            async_load = cancellation;
            image_location = url;
            IsErrored = false;

            // InitialImage is what the box shows WHILE the load runs -- a spinner or placeholder. It
            // was stored and read by nothing, so an async load showed the previous image (or nothing)
            // until it finished.
            if (InitialImage is { } placeholder)
                Image = placeholder;

            OnLoadProgressChanged (new ProgressChangedEventArgs (0, null));

            _ = Task.Run (async () => {
                Exception? failure = null;
                SKBitmap? bitmap = null;

                try {
                    bitmap = await ReadAsync (url, cancellation.Token).ConfigureAwait (false);

                    if (bitmap is null)
                        failure = new InvalidOperationException ($"The image at '{url}' could not be decoded.");
                } catch (OperationCanceledException) {
                    // Cancellation is reported through AsyncCompletedEventArgs.Cancelled, not as an error.
                } catch (Exception ex) {
                    failure = ex;
                }

                var cancelled = cancellation.IsCancellationRequested;

                // The completion has to land on the UI thread: handlers touch controls, and this is a
                // thread-pool continuation. Marshalling makes the raise ordered with the rest of the
                // queue, which is also what lets a test pump for it.
                RunOnUiThread (() => {
                    if (cancelled) {
                        bitmap?.Dispose ();
                    } else if (failure is null) {
                        InstallLoadedImage (bitmap);
                    } else {
                        bitmap?.Dispose ();
                        IsErrored = true;

                        // The same failure image the synchronous path shows (W6 mechanisms).
                        ShowErrorImage ();
                    }

                    if (ReferenceEquals (async_load, cancellation))
                        async_load = null;

                    cancellation.Dispose ();

                    if (!cancelled)
                        OnLoadProgressChanged (new ProgressChangedEventArgs (100, null));

                    OnLoadCompleted (new AsyncCompletedEventArgs (failure, cancelled, null));
                });
            });
        }

        /// <summary>Cancels an asynchronous image load started by <see cref="LoadAsync(string)"/>.</summary>
        public void CancelAsync ()
        {
            var pending = async_load;

            if (pending is null)
                return;

            async_load = null;
            pending.Cancel ();
        }

        /// <summary>Raises the <see cref="LoadCompleted"/> event.</summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnLoadCompleted (AsyncCompletedEventArgs e) => LoadCompleted?.Invoke (this, e);

        /// <summary>Raises the <see cref="LoadProgressChanged"/> event.</summary>
        /// <param name="e">The event data.</param>
        protected virtual void OnLoadProgressChanged (ProgressChangedEventArgs e) => LoadProgressChanged?.Invoke (this, e);

        // The one place that reads an image location. `rethrow` is the difference between Load (which
        // promises a failure you can catch) and the ImageLocation setter (which has no way to report
        // one, so it records IsErrored and paints the error mark).
        private void LoadCore (string? url, bool rethrow)
        {
            CancelAsync ();

            if (url is null) {
                image_location = null;
                SetLoadedImage (null);
                return;
            }

            image_location = url;
            IsErrored = false;

            try {
                var bitmap = Read (url);

                // Decode returns null for a file that is missing or not an image -- it does not throw.
                // Taking that at face value is what made a missing local file paint nothing at all
                // while IsErrored stayed false.
                if (bitmap is null)
                    throw new InvalidOperationException ($"The image at '{url}' could not be decoded.");

                InstallLoadedImage (bitmap);
            } catch (Exception) {
                IsErrored = true;

                // ErrorImage is what the box shows when a load fails (W6 mechanisms). It was stored
                // and read by nothing, so a broken path painted an empty box with no sign of why.
                ShowErrorImage ();

                if (rethrow)
                    throw;
            }
        }

        // The failure image, or an empty box when the application supplied none.
        private void ShowErrorImage ()
        {
            if (ErrorImage is { } failed)
                Image = failed;
            else
                SetLoadedImage (null);

            // Assigning Image clears IsErrored -- it is the "a picture was set" path. The box really
            // is in the failed state, so the flag goes back on after the image lands.
            IsErrored = true;
            Invalidate ();
        }

        private static SKBitmap? Read (string url)
        {
            if (IsRemote (url))
                // GetAwaiter ().GetResult () rather than .Result: it surfaces the inner exception
                // (HttpRequestException) instead of wrapping it in an AggregateException, so the
                // catch an upstream app already has around Load still matches.
                return SKBitmap.Decode (Client.GetByteArrayAsync (url).GetAwaiter ().GetResult ());

            // Open the file ourselves rather than handing the path to Decode: a missing file then
            // raises FileNotFoundException, which is what upstream's callers catch.
            using var stream = File.OpenRead (url);

            return SKBitmap.Decode (stream);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage ("Reliability", "CA2016:Forward the CancellationToken parameter",
            Justification = "netstandard2.0's HttpClient.GetByteArrayAsync takes no CancellationToken; the token is honoured around the call instead.")]
        private static async Task<SKBitmap?> ReadAsync (string url, CancellationToken cancellation)
        {
            if (IsRemote (url)) {
                // netstandard2.0's HttpClient has no cancellable GetByteArrayAsync overload, so the
                // token is checked either side of the download rather than threaded into it: a cancel
                // stops the decode and the install, not the transfer already in flight.
                var bytes = await Client.GetByteArrayAsync (url).ConfigureAwait (false);

                cancellation.ThrowIfCancellationRequested ();

                return SKBitmap.Decode (bytes);
            }

            using var stream = File.OpenRead (url);

            cancellation.ThrowIfCancellationRequested ();

            return SKBitmap.Decode (stream);
        }

        private static bool IsRemote (string url) => url.Contains ("://");

        private void InstallLoadedImage (SKBitmap? bitmap)
        {
            IsErrored = false;
            SetLoadedImage (bitmap);
        }

        private void SetLoadedImage (SKBitmap? bitmap)
        {
            _systemImage = null;
            _skImage?.Dispose ();
            _skImage = bitmap;
            UpdateSize ();
            Invalidate ();
        }

        private void RunOnUiThread (Action action)
        {
            if (InvokeRequired)
                BeginInvoke (action);
            else
                action ();
        }
    }
}

#pragma warning restore CA1416
