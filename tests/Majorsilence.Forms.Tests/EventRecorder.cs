using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Majorsilence.Forms.Tests;

/// <summary>
/// Records the events a control or window raises, in order, so a test can assert an entire sequence in
/// one assertion.
/// </summary>
/// <remarks>
/// <para>
/// Several of the divergences in <c>docs/behaviour-gap-plan.md</c> are <em>ordering</em> bugs: each
/// individual event fires, and fires with the right arguments, but not in WinForms' order. A focus
/// change raises the entering control's <c>Enter</c> before the leaving control's <c>Leave</c>; a
/// double-click raises <c>Click</c> twice. A suite of one-event-per-test assertions passes happily
/// against all of that, which is roughly why it survived this long.
/// </para>
/// <para>
/// So: subscribe once, act, then compare the whole list. Prefer
/// <see cref="AssertSequence(string[])"/> over a series of "did this fire" checks -- the failure
/// message prints expected and actual side by side, which is what makes an ordering bug legible.
/// </para>
/// <example>
/// <code>
/// using var recorder = EventRecorder.For (button, "MouseDown", "Click", "MouseClick", "MouseUp");
/// HeadlessRenderer.Click (form, x, y);
/// recorder.AssertSequence ("MouseDown", "Click", "MouseClick", "MouseUp");
/// </code>
/// </example>
/// </remarks>
public sealed class EventRecorder : IDisposable
{
    private readonly List<string> _entries = [];
    private readonly List<Action> _detach = [];
    private readonly object _gate = new ();

    private EventRecorder () { }

    /// <summary>The event names recorded so far, in the order they were raised.</summary>
    public IReadOnlyList<string> Entries {
        get { lock (_gate) return [.. _entries]; }
    }

    /// <summary>
    /// Watches the named events on <paramref name="source"/>. Names are the event names as declared
    /// (<c>"MouseDown"</c>, <c>"Enter"</c>); an event the type does not declare is an error rather than
    /// a silent no-match, because a typo would otherwise read as "this never fired".
    /// </summary>
    public static EventRecorder For (object source, params string[] eventNames)
    {
        ArgumentNullException.ThrowIfNull (source);
        ArgumentNullException.ThrowIfNull (eventNames);

        var recorder = new EventRecorder ();
        recorder.Watch (source, null, eventNames);
        return recorder;
    }

    /// <summary>
    /// Watches events on a second object, tagging them so the merged sequence stays readable -- the
    /// usual case being two controls in one focus change.
    /// </summary>
    /// <param name="label">
    /// Prefix for this source's entries, e.g. <c>"a"</c> produces <c>"a.Leave"</c>.
    /// </param>
    public EventRecorder Also (object source, string label, params string[] eventNames)
    {
        ArgumentNullException.ThrowIfNull (source);
        ArgumentException.ThrowIfNullOrEmpty (label);
        ArgumentNullException.ThrowIfNull (eventNames);

        Watch (source, label, eventNames);
        return this;
    }

    private void Watch (object source, string? label, string[] eventNames)
    {
        var type = source.GetType ();

        foreach (var name in eventNames) {
            var info = type.GetEvent (name)
                ?? throw new ArgumentException ($"{type.Name} declares no event named '{name}'.", nameof (eventNames));

            var handlerType = info.EventHandlerType
                ?? throw new ArgumentException ($"{type.Name}.{name} has no handler type.", nameof (eventNames));

            var entry = label is null ? name : $"{label}.{name}";
            var handler = BuildHandler (handlerType, entry);

            info.AddEventHandler (source, handler);
            _detach.Add (() => info.RemoveEventHandler (source, handler));
        }
    }

    /// <summary>
    /// Builds a handler of exactly <paramref name="handlerType"/>'s shape whose body records
    /// <paramref name="entry"/> and ignores its arguments.
    /// </summary>
    /// <remarks>
    /// Compiled from an expression tree rather than bound with <c>Delegate.CreateDelegate</c>. The
    /// latter needs the target method's parameter types to match the delegate's, so one
    /// <c>(object?, EventArgs)</c> method cannot serve <c>KeyEventHandler</c>,
    /// <c>MouseEventHandler</c> and the rest -- it binds nothing and returns null. Generating a lambda
    /// per shape sidesteps the question and works for any delegate that returns void, including the
    /// framework's <c>EventHandler&lt;T&gt;</c> and its hand-written handler types.
    /// </remarks>
    private Delegate BuildHandler (Type handlerType, string entry)
    {
        var invoke = handlerType.GetMethod ("Invoke")
            ?? throw new ArgumentException ($"{handlerType.Name} has no Invoke method.", nameof (handlerType));

        if (invoke.ReturnType != typeof (void))
            throw new ArgumentException (
                $"{handlerType.Name} returns {invoke.ReturnType.Name}; the recorder only handles void "
                + "event handlers.", nameof (handlerType));

        var parameters = invoke.GetParameters ()
            .Select (p => System.Linq.Expressions.Expression.Parameter (p.ParameterType, p.Name))
            .ToArray ();

        var body = System.Linq.Expressions.Expression.Call (
            System.Linq.Expressions.Expression.Constant (this),
            typeof (EventRecorder).GetMethod (nameof (Record),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!,
            System.Linq.Expressions.Expression.Constant (entry));

        return System.Linq.Expressions.Expression
            .Lambda (handlerType, body, parameters)
            .Compile ();
    }

    private void Record (string entry)
    {
        lock (_gate) _entries.Add (entry);
    }

    /// <summary>Forgets everything recorded so far, so one test can assert several sequences.</summary>
    public void Clear ()
    {
        lock (_gate) _entries.Clear ();
    }

    /// <summary>Asserts the full recorded sequence equals <paramref name="expected"/>, in order.</summary>
    public void AssertSequence (params string[] expected)
    {
        var actual = Entries;

        Assert.True (
            expected.Length == actual.Count && expected.SequenceEqual (actual),
            "Event sequence did not match.\n"
            + $"  expected: {Describe (expected)}\n"
            + $"  actual:   {Describe (actual)}");
    }

    /// <summary>
    /// Asserts the recorded sequence contains these entries in this relative order, ignoring anything
    /// else in between. For sequences where only the relative order of two events is the contract.
    /// </summary>
    public void AssertOrder (params string[] expected)
    {
        var actual = Entries;
        var index = -1;

        foreach (var name in expected) {
            var next = -1;
            for (var i = index + 1; i < actual.Count; i++) {
                if (actual[i] == name) { next = i; break; }
            }

            Assert.True (next >= 0,
                $"Expected '{name}' after position {index} but it was not raised.\n"
                + $"  expected order: {Describe (expected)}\n"
                + $"  actual:         {Describe (actual)}");

            index = next;
        }
    }

    /// <summary>How many times an event was raised -- for "fired exactly once" assertions.</summary>
    public int Count (string entry) => Entries.Count (e => e == entry);

    private static string Describe (IReadOnlyList<string> entries)
        => entries.Count == 0 ? "(nothing)" : string.Join (" -> ", entries);

    public void Dispose ()
    {
        foreach (var detach in _detach)
            detach ();

        _detach.Clear ();
    }
}
