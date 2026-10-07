using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Majorsilence.Forms.Analyzers
{
    /// <summary>
    /// Flags calls that block the UI thread in code that runs on the browser (WebAssembly) target, where
    /// .NET runs on the page's only thread and nothing may block it (issue #406):
    /// <list type="bullet">
    /// <item><c>MFB001</c> -- a blocking modal call (<c>Form.ShowDialog</c>, <c>MessageBox.Show</c>, the
    /// file/folder pickers' <c>ShowDialog</c>, <c>TaskDialog.ShowDialog</c>, <c>VbInteraction.MsgBox</c>, …).
    /// These throw <see cref="PlatformNotSupportedException"/> there; each has an awaitable twin.</item>
    /// <item><c>MFB002</c> -- waiting on a task synchronously (<c>.Result</c>, <c>.Wait ()</c>,
    /// <c>.GetAwaiter ().GetResult ()</c>, <c>Task.WaitAll/WaitAny</c>), which on the one thread that
    /// would complete the task can never return.</item>
    /// <item><c>MFB003</c> -- <c>Thread.Sleep</c>, which freezes the page for its duration.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Silent unless the code is browser code: compiled for a <c>-browser</c> target framework (the SDK
    /// defines <c>BROWSER</c>), a library that declares <c>&lt;SupportedPlatform Include="browser" /&gt;</c>,
    /// or opted in with <c>majorsilence_forms.browser_target = true</c> in an <c>.editorconfig</c> /
    /// <c>.globalconfig</c> -- the way to cover a shared UI library that a browser head references.
    /// </remarks>
    [DiagnosticAnalyzer (LanguageNames.CSharp)]
    public sealed class BrowserBlockingCallAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>A blocking modal call.</summary>
        public const string BlockingModalId = "MFB001";

        /// <summary>A synchronous wait on a task.</summary>
        public const string SyncOverAsyncId = "MFB002";

        /// <summary><c>Thread.Sleep</c>.</summary>
        public const string ThreadSleepId = "MFB003";

        /// <summary>The opt-in option for code that is not itself compiled for the browser.</summary>
        public const string OptInOption = "majorsilence_forms.browser_target";

        /// <summary>Diagnostic property: the awaitable method to call instead (MFB001).</summary>
        internal const string AsyncNameProperty = "AsyncName";

        private const string Category = "Usage";
        private const string HelpLink = "https://github.com/majorsilence/Majorsilence.Forms/blob/main/docs/backends.md#browser-threading";

        internal static readonly DiagnosticDescriptor BlockingModal = new (
            BlockingModalId,
            "Blocking modal call on the browser target",
            "'{0}' blocks until the dialog closes, which the browser target cannot do (it throws PlatformNotSupportedException there); await '{1}' instead",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "In the browser, .NET runs on the page's only thread; a call that does not return also stops the events that would close the dialog.",
            helpLinkUri: HelpLink);

        internal static readonly DiagnosticDescriptor SyncOverAsync = new (
            SyncOverAsyncId,
            "Synchronous wait on a task on the browser target",
            "'{0}' blocks the browser's only thread, which is the thread that would complete the task; await it instead",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "Blocking the main thread is not supported by browsers; the wait either throws or never returns.",
            helpLinkUri: HelpLink);

        internal static readonly DiagnosticDescriptor ThreadSleep = new (
            ThreadSleepId,
            "Thread.Sleep on the browser target",
            "'Thread.Sleep' freezes the page for its whole duration on the browser target; use 'await Task.Delay' instead",
            Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: "The browser cannot paint or deliver input while the page's only thread sleeps.",
            helpLinkUri: HelpLink);

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create (BlockingModal, SyncOverAsync, ThreadSleep);

        /// <inheritdoc/>
        public override void Initialize (AnalysisContext context)
        {
            context.EnableConcurrentExecution ();
            context.ConfigureGeneratedCodeAnalysis (GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction (start => {
                var wholeCompilation = IsBrowserCompilation (start.Compilation, start.Options.AnalyzerConfigOptionsProvider);
                var provider = start.Options.AnalyzerConfigOptionsProvider;

                // An .editorconfig section can opt in some files only, so without a project-wide signal the
                // decision is per tree.
                bool Enabled (SyntaxTree? tree) =>
                    wholeCompilation || tree is not null && IsTrue (provider.GetOptions (tree), OptInOption);

                if (!wholeCompilation && !start.Compilation.SyntaxTrees.Any (t => Enabled (t)))
                    return;

                var types = new KnownTypes (start.Compilation);

                start.RegisterOperationAction (op => {
                    if (Enabled (op.Operation.Syntax.SyntaxTree))
                        AnalyzeInvocation (op, (IInvocationOperation) op.Operation, types);
                }, OperationKind.Invocation);

                start.RegisterOperationAction (op => {
                    if (Enabled (op.Operation.Syntax.SyntaxTree))
                        AnalyzePropertyReference (op, (IPropertyReferenceOperation) op.Operation, types);
                }, OperationKind.PropertyReference);
            });
        }

        // The SDK defines BROWSER for a net*-browser target framework; build_property.TargetFramework is
        // visible to analyzers by default (the platform-compatibility analyzer reads it), and so is the
        // SupportedPlatform list a browser-capable library declares.
        internal static bool IsBrowserCompilation (Compilation compilation, AnalyzerConfigOptionsProvider provider)
        {
            if (compilation.SyntaxTrees.Any (t => t.Options.PreprocessorSymbolNames.Contains ("BROWSER")))
                return true;

            var global = provider.GlobalOptions;

            if (global.TryGetValue ("build_property.TargetFramework", out var tfm) && tfm.EndsWith ("-browser", StringComparison.OrdinalIgnoreCase))
                return true;

            if (global.TryGetValue ("build_property._SupportedPlatformList", out var platforms)
                && platforms.Split (',', ';').Any (p => p.Trim ().Equals ("browser", StringComparison.OrdinalIgnoreCase)))
                return true;

            return IsTrue (global, OptInOption);
        }

        private static bool IsTrue (AnalyzerConfigOptions options, string key) =>
            options.TryGetValue (key, out var value) && value.Trim ().Equals ("true", StringComparison.OrdinalIgnoreCase);

        private static void AnalyzeInvocation (OperationAnalysisContext context, IInvocationOperation invocation, KnownTypes types)
        {
            var method = invocation.TargetMethod;

            if (AsyncTwinName (method) is { } asyncName) {
                var hasTwin = FindTwin (method, asyncName) is not null;
                var properties = ImmutableDictionary<string, string?>.Empty.Add (AsyncNameProperty, hasTwin ? asyncName : null);

                context.ReportDiagnostic (Diagnostic.Create (BlockingModal, invocation.Syntax.GetLocation (), properties,
                    method.ContainingType.Name + "." + method.Name, method.ContainingType.Name + "." + asyncName));
                return;
            }

            if (types.Thread is not null && method.Name == "Sleep" && SymbolEqualityComparer.Default.Equals (method.ContainingType, types.Thread)) {
                context.ReportDiagnostic (Diagnostic.Create (ThreadSleep, invocation.Syntax.GetLocation ()));
                return;
            }

            if (types.IsTask (method.ContainingType) && (method.Name == "Wait" || method.Name == "WaitAll" || method.Name == "WaitAny")) {
                context.ReportDiagnostic (Diagnostic.Create (SyncOverAsync, invocation.Syntax.GetLocation (), "Task." + method.Name));
                return;
            }

            if (method.Name == "GetResult" && types.IsAwaiter (method.ContainingType))
                context.ReportDiagnostic (Diagnostic.Create (SyncOverAsync, invocation.Syntax.GetLocation (), "GetAwaiter ().GetResult ()"));
        }

        private static void AnalyzePropertyReference (OperationAnalysisContext context, IPropertyReferenceOperation reference, KnownTypes types)
        {
            if (reference.Property.Name == "Result" && types.IsTask (reference.Property.ContainingType))
                context.ReportDiagnostic (Diagnostic.Create (SyncOverAsync, reference.Syntax.GetLocation (), "Task.Result"));
        }

        // The awaitable method a blocking modal call should become, or null when the call does not block.
        internal static string? AsyncTwinName (IMethodSymbol method)
        {
            var type = method.ContainingType;

            if (type is null || !InFormsNamespace (type))
                return null;

            switch (method.Name) {
            case "ShowDialog":
                // UI-less stubs: their ShowDialog answers OK at once and never blocks.
                for (var t = type; t is not null; t = t.BaseType)
                    if (t.Name is "PrintDialog" or "PageSetupDialog" or "SchedulerPrintSettingsDialog" && InFormsNamespace (t))
                        return null;
                return "ShowDialogAsync";
            case "ShowDialogSync":
                return "ShowDialogAsync";
            case "Show" when type.Name is "MessageBox" or "RadMessageBox":
                return "ShowAsync";
            case "MsgBox" or "InputBox" when type.Name == "VbInteraction":
                return method.Name + "Async";
            default:
                return null;
            }
        }

        private static bool InFormsNamespace (INamedTypeSymbol type)
        {
            var ns = type.ContainingNamespace?.ToDisplayString () ?? string.Empty;
            return ns == "Majorsilence.Forms" || ns.StartsWith ("Majorsilence.Forms.", StringComparison.Ordinal);
        }

        /// <summary>The awaitable overload taking the same parameters, searched from the call's type upward.</summary>
        internal static IMethodSymbol? FindTwin (IMethodSymbol method, string asyncName)
        {
            for (var type = method.ContainingType; type is not null; type = type.BaseType) {
                foreach (var candidate in type.GetMembers (asyncName).OfType<IMethodSymbol> ()) {
                    if (candidate.IsStatic == method.IsStatic
                        && candidate.DeclaredAccessibility == Accessibility.Public
                        && candidate.Parameters.Length == method.Parameters.Length
                        && candidate.Parameters.Zip (method.Parameters, (a, b) => SymbolEqualityComparer.Default.Equals (a.Type, b.Type)).All (same => same))
                        return candidate;
                }
            }

            return null;
        }

        private sealed class KnownTypes
        {
            private readonly INamedTypeSymbol? task;
            private readonly List<INamedTypeSymbol> awaiters = new ();

            public KnownTypes (Compilation compilation)
            {
                task = compilation.GetTypeByMetadataName ("System.Threading.Tasks.Task");
                Thread = compilation.GetTypeByMetadataName ("System.Threading.Thread");

                foreach (var name in new[] {
                    "System.Runtime.CompilerServices.TaskAwaiter",
                    "System.Runtime.CompilerServices.TaskAwaiter`1",
                    "System.Runtime.CompilerServices.ConfiguredTaskAwaitable+ConfiguredTaskAwaiter",
                    "System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter",
                    "System.Runtime.CompilerServices.ValueTaskAwaiter`1",
                    "System.Runtime.CompilerServices.ValueTaskAwaiter",
                }) {
                    if (compilation.GetTypeByMetadataName (name) is { } t)
                        awaiters.Add (t);
                }
            }

            public INamedTypeSymbol? Thread { get; }

            public bool IsTask (INamedTypeSymbol? type)
            {
                for (var t = type; t is not null; t = t.BaseType)
                    if (SymbolEqualityComparer.Default.Equals (t.OriginalDefinition, task))
                        return true;
                return false;
            }

            public bool IsAwaiter (INamedTypeSymbol? type) =>
                type is not null && awaiters.Any (a => SymbolEqualityComparer.Default.Equals (type.OriginalDefinition, a));
        }
    }
}
