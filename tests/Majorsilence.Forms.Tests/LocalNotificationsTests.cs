using System;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Backends;
using Majorsilence.Forms.Headless;
using Majorsilence.Forms.Notifications;
using Xunit;

namespace Majorsilence.Forms.Tests;

// LocalNotifications (register item F14's Android half): a reported no-op everywhere but Android, which
// has no fake to exercise here -- Platform.Backend is swapped for a minimal recording fake instead, the
// same technique HapticsTests/ReducedMotionTests.NotReducedMotionAware use to test the "backend does not
// implement this" path, and to raise Tapped/PermissionChanged the same way F10/F11 raise a backend event
// directly since nothing in Headless has a real notification centre or permission dialog to trigger it.
[Collection ("Headless")]
public class LocalNotificationsTests
{
    [Fact]
    public void IsSupported_is_false_on_Headless ()
    {
        HeadlessRenderer.Use ();

        Assert.False (LocalNotifications.IsSupported);
    }

    [Fact]
    public void IsPermissionGranted_is_false_on_Headless ()
    {
        HeadlessRenderer.Use ();

        Assert.False (LocalNotifications.IsPermissionGranted);
    }

    [Fact]
    public void Every_member_is_a_safe_no_op_on_Headless ()
    {
        HeadlessRenderer.Use ();

        LocalNotifications.RequestPermission ();
        LocalNotifications.RegisterChannel (new NotificationChannel ("alarm", "Alarm"));
        LocalNotifications.Show (1, new LocalNotification { ChannelId = "alarm", Title = "Warm", Text = "The workshop is warm" });
        LocalNotifications.Cancel (1);
    }

    [Fact]
    public void RegisterChannel_refuses_a_missing_channel ()
    {
        HeadlessRenderer.Use ();

        Assert.Throws<ArgumentNullException> (() => LocalNotifications.RegisterChannel (null!));
    }

    [Fact]
    public void Show_refuses_a_missing_notification ()
    {
        HeadlessRenderer.Use ();

        Assert.Throws<ArgumentNullException> (() => LocalNotifications.Show (1, null!));
    }

    [Fact]
    public void IsSupported_is_true_when_the_backend_implements_INotificationBackend ()
    {
        var previous = Platform.ConfiguredBackend;
        Platform.Backend = new RecordingNotifications ();
        try {
            Assert.True (LocalNotifications.IsSupported);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void IsPermissionGranted_reaches_the_backend ()
    {
        var previous = Platform.ConfiguredBackend;
        var backend = new RecordingNotifications { PermissionGranted = true };
        Platform.Backend = backend;
        try {
            Assert.True (LocalNotifications.IsPermissionGranted);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void IsPermissionGranted_reflects_a_denied_backend ()
    {
        // Proves this reads backend.IsPermissionGranted itself, not merely whether a backend is present --
        // the two are easy to conflate since IsSupported is exactly the latter.
        var previous = Platform.ConfiguredBackend;
        var backend = new RecordingNotifications { PermissionGranted = false };
        Platform.Backend = backend;
        try {
            Assert.False (LocalNotifications.IsPermissionGranted);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void RequestPermission_reaches_the_backend ()
    {
        var previous = Platform.ConfiguredBackend;
        var backend = new RecordingNotifications ();
        Platform.Backend = backend;
        try {
            LocalNotifications.RequestPermission ();
            Assert.Equal (1, backend.PermissionRequests);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void RegisterChannel_reaches_the_backend ()
    {
        var previous = Platform.ConfiguredBackend;
        var backend = new RecordingNotifications ();
        Platform.Backend = backend;
        try {
            var channel = new NotificationChannel ("alarm", "Alarm") { Description = "Loud", Importance = NotificationImportance.High, Sound = false };
            LocalNotifications.RegisterChannel (channel);
            Assert.Equal ("alarm", backend.LastChannelId);
            Assert.Equal ("Alarm", backend.LastChannelName);
            Assert.Equal ("Loud", backend.LastChannelDescription);
            Assert.Equal (NotificationImportance.High, backend.LastChannelImportance);
            Assert.False (backend.LastChannelSound);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void Show_reaches_the_backend_with_the_given_id ()
    {
        var previous = Platform.ConfiguredBackend;
        var backend = new RecordingNotifications ();
        Platform.Backend = backend;
        try {
            var notification = new LocalNotification { ChannelId = "alarm", Title = "Warm", Text = "The workshop is warm", Ongoing = true, FullScreen = true };
            LocalNotifications.Show (7, notification);
            Assert.Equal (7, backend.LastShownId);
            Assert.Equal ("alarm", backend.LastShownChannelId);
            Assert.Equal ("Warm", backend.LastShownTitle);
            Assert.Equal ("The workshop is warm", backend.LastShownText);
            Assert.True (backend.LastShownOngoing);
            Assert.True (backend.LastShownFullScreen);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void Cancel_reaches_the_backend_with_the_given_id ()
    {
        var previous = Platform.ConfiguredBackend;
        var backend = new RecordingNotifications ();
        Platform.Backend = backend;
        try {
            LocalNotifications.Cancel (9);
            Assert.Equal (9, backend.LastCancelledId);
        } finally {
            RestoreBackend (previous);
        }
    }

    [Fact]
    public void Tapped_carries_the_notifications_id ()
    {
        HeadlessRenderer.Use ();

        NotificationTappedEventArgs? seen = null;
        LocalNotifications.Tapped += (_, e) => seen = e;

        LocalNotifications.RaiseTapped (42);

        Assert.NotNull (seen);
        Assert.Equal (42, seen!.Id);
    }

    [Fact]
    public void PermissionChanged_is_raised ()
    {
        HeadlessRenderer.Use ();

        var raised = 0;
        LocalNotifications.PermissionChanged += (_, _) => raised++;

        LocalNotifications.RaisePermissionChanged ();

        Assert.Equal (1, raised);
    }

    [Fact]
    public void NotificationChannel_refuses_missing_arguments ()
    {
        Assert.Throws<ArgumentNullException> (() => new NotificationChannel (null!, "Alarm"));
        Assert.Throws<ArgumentNullException> (() => new NotificationChannel ("alarm", null!));
    }

    [Fact]
    public void NotificationChannel_defaults_to_Default_importance_and_sound_on ()
    {
        var channel = new NotificationChannel ("alarm", "Alarm");

        Assert.Equal (NotificationImportance.Default, channel.Importance);
        Assert.True (channel.Sound);
    }

    private static void RestoreBackend (IPlatformBackend? previous)
    {
        if (previous is not null)
            Platform.Backend = previous;
        else
            HeadlessRenderer.Use ();
    }

    // A minimal IPlatformBackend + INotificationBackend fake, standing in for Android: nothing but the
    // notification members needs to actually work, the rest matches NotReducedMotionAware's own "never
    // called" contract.
    private sealed class RecordingNotifications : IPlatformBackend, INotificationBackend
    {
        public bool PermissionGranted;
        public int PermissionRequests;
        public string? LastChannelId;
        public string? LastChannelName;
        public string? LastChannelDescription;
        public NotificationImportance LastChannelImportance;
        public bool LastChannelSound;
        public int LastShownId = -1;
        public string? LastShownChannelId;
        public string? LastShownTitle;
        public string? LastShownText;
        public bool LastShownOngoing;
        public bool LastShownFullScreen;
        public int LastCancelledId = -1;

        public bool IsPermissionGranted => PermissionGranted;
        public void RequestPermission () => PermissionRequests++;
        public void RegisterChannel (string id, string name, string? description, NotificationImportance importance, bool sound)
        {
            LastChannelId = id;
            LastChannelName = name;
            LastChannelDescription = description;
            LastChannelImportance = importance;
            LastChannelSound = sound;
        }
        public void Show (int id, string channelId, string title, string text, bool ongoing, bool fullScreen)
        {
            LastShownId = id;
            LastShownChannelId = channelId;
            LastShownTitle = title;
            LastShownText = text;
            LastShownOngoing = ongoing;
            LastShownFullScreen = fullScreen;
        }
        public void Cancel (int id) => LastCancelledId = id;

        public string Name => "recording notifications";
        public void Initialize () => throw new NotImplementedException ();
        public void RunMainLoop (CancellationToken token) => throw new NotImplementedException ();
        public void Stop () => throw new NotImplementedException ();
        public void Post (Action action) => throw new NotImplementedException ();
        public void Invoke (Action action) => throw new NotImplementedException ();
        public T Invoke<T> (Func<T> func) => throw new NotImplementedException ();
        public bool CheckAccess () => throw new NotImplementedException ();
        public void DoEvents () => throw new NotImplementedException ();
        public IWindowBackend CreateWindow (WindowBase owner, bool isPopup) => throw new NotImplementedException ();
        public IPlatformTimer CreateTimer () => throw new NotImplementedException ();
        public string GetClipboardText () => throw new NotImplementedException ();
        public void SetClipboardText (string text) => throw new NotImplementedException ();
        public void ClearClipboard () => throw new NotImplementedException ();
        public ScreenInfo[] GetScreens () => throw new NotImplementedException ();
        public void RunModalLoop (Task completed) => throw new NotImplementedException ();
    }
}
