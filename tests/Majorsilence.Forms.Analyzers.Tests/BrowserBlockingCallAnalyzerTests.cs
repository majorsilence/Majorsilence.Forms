using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Majorsilence.Forms.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Majorsilence.Forms.Analyzers.Tests;

// The analyzer behind issue #406's migration help: in browser code it flags the calls that block the UI
// thread, and offers the awaited form where that rewrite is safe. The source under test is compiled
// against the real Majorsilence.Forms assembly, so the symbols it resolves are the ones a user's are.
public class BrowserBlockingCallAnalyzerTests
{
    private static readonly MetadataReference[] references = ((string) AppContext.GetData ("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split (Path.PathSeparator)
        .Where (p => p.EndsWith (".dll", StringComparison.OrdinalIgnoreCase))
        .Select (p => (MetadataReference) MetadataReference.CreateFromFile (p))
        .ToArray ();

    private const string Usings = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Majorsilence.Forms;

        """;

    private sealed class EditorConfig (Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue (string key, out string value) => values.TryGetValue (key, out value!);
    }

    private sealed class Options (AnalyzerConfigOptions global, AnalyzerConfigOptions perTree) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => global;
        public override AnalyzerConfigOptions GetOptions (SyntaxTree tree) => perTree;
        public override AnalyzerConfigOptions GetOptions (AdditionalText textFile) => perTree;
    }

    private static Document CreateDocument (string source, bool browser)
    {
        var workspace = new AdhocWorkspace ();
        var parse = new CSharpParseOptions (LanguageVersion.Latest, preprocessorSymbols: browser ? ["BROWSER"] : []);
        var project = workspace.AddProject ("Under.Test", LanguageNames.CSharp)
            .WithParseOptions (parse)
            .WithCompilationOptions (new CSharpCompilationOptions (OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .WithMetadataReferences (references);

        return project.AddDocument ("Test.cs", SourceText.From (Usings + source));
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync (Document document, AnalyzerConfigOptionsProvider? options = null)
    {
        var compilation = (await document.Project.GetCompilationAsync (TestContext.Current.CancellationToken))!;

        // A test whose source does not compile proves nothing about the analyzer.
        Assert.Empty (compilation.GetDiagnostics (TestContext.Current.CancellationToken).Where (d => d.Severity == DiagnosticSeverity.Error));

        var analyzerOptions = new AnalyzerOptions ([], options ?? new Options (new EditorConfig ([]), new EditorConfig ([])));

        return await compilation
            .WithAnalyzers ([new BrowserBlockingCallAnalyzer ()], analyzerOptions)
            .GetAnalyzerDiagnosticsAsync (TestContext.Current.CancellationToken);
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync (string source, bool browser = true) =>
        AnalyzeAsync (CreateDocument (source, browser));

    private static async Task<string?> FixAsync (string source, string id)
    {
        var document = CreateDocument (source, browser: true);
        var diagnostic = Assert.Single (await AnalyzeAsync (document), d => d.Id == id);

        var actions = new List<CodeAction> ();
        var context = new CodeFixContext (document, diagnostic, (action, _) => actions.Add (action), TestContext.Current.CancellationToken);
        await new BrowserBlockingCallCodeFix ().RegisterCodeFixesAsync (context);

        if (actions.Count == 0)
            return null;

        var operations = await Assert.Single (actions).GetOperationsAsync (TestContext.Current.CancellationToken);
        var changed = operations.OfType<ApplyChangesOperation> ().Single ().ChangedSolution.GetDocument (document.Id)!;

        // The fixed code must compile and must no longer carry the diagnostic.
        var after = await AnalyzeAsync (changed);
        Assert.DoesNotContain (after, d => d.Id == id);

        var text = (await changed.GetTextAsync (TestContext.Current.CancellationToken)).ToString ();
        return text.Substring (Usings.Length);
    }

    // ── What is flagged ──

    [Fact]
    public async Task Blocking_modal_calls_are_flagged_with_their_awaitable_twin ()
    {
        var diagnostics = await AnalyzeAsync ("""
            class Editor : Form
            {
                void Run (Form dialog, OpenFileDialog open, FolderBrowserDialog folder, TaskDialogPage page)
                {
                    dialog.ShowDialog (this);
                    MessageBox.Show ("Saved");
                    open.ShowDialog (this);
                    folder.ShowDialog ();
                    TaskDialog.ShowDialog (page);
                    VbInteraction.MsgBox ("Hello");
                    VbInteraction.InputBox ("Name?");
                    Majorsilence.Forms.Telerik.RadMessageBox.Show ("Exported");
                }
            }
            """);

        Assert.All (diagnostics, d => Assert.Equal (BrowserBlockingCallAnalyzer.BlockingModalId, d.Id));
        Assert.Equal (new[] {
            "ShowDialogAsync", "ShowAsync", "ShowDialogAsync", "ShowDialogAsync", "ShowDialogAsync",
            "MsgBoxAsync", "InputBoxAsync", "ShowAsync",
        }, diagnostics.OrderBy (d => d.Location.SourceSpan.Start).Select (d => d.Properties[BrowserBlockingCallAnalyzer.AsyncNameProperty]));
        Assert.Contains ("MessageBox.ShowAsync", diagnostics.Single (d => d.GetMessage ().StartsWith ("'MessageBox.Show'", StringComparison.Ordinal)).GetMessage ());
    }

    [Fact]
    public async Task UI_less_print_stubs_and_the_async_forms_are_not_flagged ()
    {
        var diagnostics = await AnalyzeAsync ("""
            class Editor : Form
            {
                async Task Run (Form dialog, PrintDialog print, PageSetupDialog setup)
                {
                    print.ShowDialog ();     // answers OK at once; never blocks
                    setup.ShowDialog ();
                    await dialog.ShowDialogAsync (this);
                    await MessageBox.ShowAsync ("Saved");
                    Show ();                 // modeless
                }
            }
            """);

        Assert.Empty (diagnostics);
    }

    [Fact]
    public async Task Sync_over_async_and_Thread_Sleep_are_flagged ()
    {
        var diagnostics = await AnalyzeAsync ("""
            class Worker
            {
                int Run (Task<int> work, Task other, ValueTask<int> value)
                {
                    other.Wait ();
                    Task.WaitAll (other);
                    var a = work.Result;
                    var b = work.GetAwaiter ().GetResult ();
                    var c = work.ConfigureAwait (false).GetAwaiter ().GetResult ();
                    var d = value.GetAwaiter ().GetResult ();
                    Thread.Sleep (100);
                    return a + b + c + d;
                }
            }
            """);

        Assert.Equal (6, diagnostics.Count (d => d.Id == BrowserBlockingCallAnalyzer.SyncOverAsyncId));
        Assert.Single (diagnostics, d => d.Id == BrowserBlockingCallAnalyzer.ThreadSleepId);
    }

    // ── When it speaks at all ──

    private const string OneBlockingCall = """
        class Editor : Form
        {
            void Run () => MessageBox.Show ("Saved");
        }
        """;

    [Fact]
    public async Task Code_not_built_for_the_browser_is_left_alone ()
    {
        // Every Majorsilence.Forms consumer gets this analyzer; on the desktop these calls are fine.
        Assert.Empty (await AnalyzeAsync (OneBlockingCall, browser: false));
        Assert.Single (await AnalyzeAsync (OneBlockingCall, browser: true));
    }

    [Theory]
    [InlineData ("build_property.TargetFramework", "net10.0-browser")]
    [InlineData ("build_property._SupportedPlatformList", "Linux,macOS,Windows,browser")]
    [InlineData (BrowserBlockingCallAnalyzer.OptInOption, "true")]
    public async Task A_project_wide_signal_turns_it_on (string key, string value)
    {
        var document = CreateDocument (OneBlockingCall, browser: false);
        var options = new Options (new EditorConfig (new () { [key] = value }), new EditorConfig ([]));

        Assert.Single (await AnalyzeAsync (document, options));
    }

    [Fact]
    public async Task An_editorconfig_section_can_opt_in_a_shared_library ()
    {
        // A UI library that a browser head references is not itself compiled for the browser.
        var document = CreateDocument (OneBlockingCall, browser: false);
        var options = new Options (new EditorConfig ([]), new EditorConfig (new () { [BrowserBlockingCallAnalyzer.OptInOption] = "true" }));

        Assert.Single (await AnalyzeAsync (document, options));
    }

    // ── The code fix ──

    [Fact]
    public async Task An_event_handler_becomes_async_void_and_awaits_the_twin ()
    {
        var fixedSource = await FixAsync ("""
            class Editor : Form
            {
                private void saveButton_Click (object sender, EventArgs e)
                {
                    if (MessageBox.Show (this, "Save?", "Editor", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        Text = "saved";
                }
            }
            """, BrowserBlockingCallAnalyzer.BlockingModalId);

        Assert.Equal ("""
            class Editor : Form
            {
                private async void saveButton_Click (object sender, EventArgs e)
                {
                    if (await MessageBox.ShowAsync (this, "Save?", "Editor", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        Text = "saved";
                }
            }
            """, fixedSource);
    }

    [Fact]
    public async Task An_already_async_method_just_gains_the_await_with_parentheses_where_needed ()
    {
        var fixedSource = await FixAsync ("""
            class Editor : Form
            {
                async Task<string> Ask (Form dialog)
                {
                    await Task.Yield ();
                    return dialog.ShowDialog (this).ToString ();
                }
            }
            """, BrowserBlockingCallAnalyzer.BlockingModalId);

        Assert.Contains ("return (await dialog.ShowDialogAsync (this)).ToString ();", fixedSource);
    }

    [Fact]
    public async Task A_lambda_event_handler_becomes_an_async_lambda ()
    {
        var fixedSource = await FixAsync ("""
            class Editor : Form
            {
                Editor ()
                {
                    var button = new Button ();
                    button.Click += (sender, e) => MessageBox.Show ("Clicked");
                }
            }
            """, BrowserBlockingCallAnalyzer.BlockingModalId);

        Assert.Contains ("button.Click += async (sender, e) => await MessageBox.ShowAsync (\"Clicked\");", fixedSource);
    }

    [Fact]
    public async Task Sync_over_async_and_sleep_become_awaits ()
    {
        var source = """
            class Worker : Form
            {
                async Task<int> Run (Task<int> work)
                {
                    Thread.Sleep (250);
                    return work.Result + 1;
                }
            }
            """;

        Assert.Contains ("await Task.Delay (250);", await FixAsync (source, BrowserBlockingCallAnalyzer.ThreadSleepId));
        Assert.Contains ("return await work + 1;", await FixAsync (source, BrowserBlockingCallAnalyzer.SyncOverAsyncId));

        var waits = await FixAsync ("""
            class Worker
            {
                async Task Run (Task work)
                {
                    work.Wait ();
                }
            }
            """, BrowserBlockingCallAnalyzer.SyncOverAsyncId);
        Assert.Contains ("await work;", waits);
    }

    [Fact]
    public async Task No_fix_where_the_rewrite_would_change_a_signature_callers_depend_on ()
    {
        // A value-returning synchronous method: making it async changes its return type and every caller.
        Assert.Null (await FixAsync ("""
            class Editor : Form
            {
                bool Confirm () => MessageBox.Show ("Sure?", "", MessageBoxButtons.YesNo) == DialogResult.Yes;
            }
            """, BrowserBlockingCallAnalyzer.BlockingModalId));

        // A constructor cannot be async at all.
        Assert.Null (await FixAsync ("""
            class Editor : Form
            {
                Editor () { MessageBox.Show ("Starting"); }
            }
            """, BrowserBlockingCallAnalyzer.BlockingModalId));

        // await is not allowed inside lock.
        Assert.Null (await FixAsync ("""
            class Editor : Form
            {
                readonly object gate = new ();
                async Task Run () { lock (gate) { MessageBox.Show ("Locked"); } await Task.Yield (); }
            }
            """, BrowserBlockingCallAnalyzer.BlockingModalId));

        // Wait (timeout) answers whether the task finished; await does not.
        Assert.Null (await FixAsync ("""
            class Worker
            {
                async Task<bool> Run (Task work) { await Task.Yield (); return work.Wait (100); }
            }
            """, BrowserBlockingCallAnalyzer.SyncOverAsyncId));
    }
}
