using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace Majorsilence.Forms.Analyzers
{
    /// <summary>
    /// Rewrites a blocking call flagged by <see cref="BrowserBlockingCallAnalyzer"/> into its awaited form:
    /// <c>dialog.ShowDialog (this)</c> → <c>await dialog.ShowDialogAsync (this)</c>,
    /// <c>MessageBox.Show (…)</c> → <c>await MessageBox.ShowAsync (…)</c>, <c>task.Result</c>,
    /// <c>task.Wait ()</c> and <c>task.GetAwaiter ().GetResult ()</c> → <c>await task</c>,
    /// <c>Thread.Sleep (n)</c> → <c>await Task.Delay (n)</c>.
    /// </summary>
    /// <remarks>
    /// Offered only where the rewrite keeps the program's shape: inside a function that is already
    /// <c>async</c>, or a <c>void</c> event handler (<c>(object sender, EventArgs e)</c>-shaped), which becomes
    /// <c>async void</c> -- the one place that is the accepted idiom. Anywhere else, making the caller async
    /// changes its signature and every caller of it, which is a decision for a person, not a light bulb.
    /// </remarks>
    [ExportCodeFixProvider (LanguageNames.CSharp, Name = nameof (BrowserBlockingCallCodeFix)), Shared]
    public sealed class BrowserBlockingCallCodeFix : CodeFixProvider
    {
        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create (
            BrowserBlockingCallAnalyzer.BlockingModalId, BrowserBlockingCallAnalyzer.SyncOverAsyncId, BrowserBlockingCallAnalyzer.ThreadSleepId);

        /// <inheritdoc/>
        public override FixAllProvider GetFixAllProvider () => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc/>
        public override async Task RegisterCodeFixesAsync (CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync (context.CancellationToken).ConfigureAwait (false);
            var model = await context.Document.GetSemanticModelAsync (context.CancellationToken).ConfigureAwait (false);

            if (root is null || model is null)
                return;

            foreach (var diagnostic in context.Diagnostics) {
                var node = root.FindNode (diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

                if (Rewrite (diagnostic, node) is not { } rewrite)
                    continue;

                if (!CanAwaitAt (rewrite.Target, model, context.CancellationToken, out var function))
                    continue;

                context.RegisterCodeFix (CodeAction.Create (
                    rewrite.Title,
                    ct => ApplyAsync (context.Document, rewrite.Target, rewrite.Awaited, function, ct),
                    equivalenceKey: diagnostic.Id),
                    diagnostic);
            }
        }

        private sealed class Replacement
        {
            public Replacement (string title, ExpressionSyntax target, ExpressionSyntax awaited)
            {
                Title = title;
                Target = target;
                Awaited = awaited;
            }

            public string Title { get; }

            /// <summary>The expression to replace.</summary>
            public ExpressionSyntax Target { get; }

            /// <summary>What to await in its place.</summary>
            public ExpressionSyntax Awaited { get; }
        }

        private static Replacement? Rewrite (Diagnostic diagnostic, SyntaxNode node)
        {
            switch (diagnostic.Id) {
            case BrowserBlockingCallAnalyzer.BlockingModalId: {
                if (!diagnostic.Properties.TryGetValue (BrowserBlockingCallAnalyzer.AsyncNameProperty, out var asyncName) || asyncName is null)
                    return null;
                if (node.FirstAncestorOrSelf<InvocationExpressionSyntax> () is not { } invocation)
                    return null;

                var renamed = invocation.Expression switch {
                    MemberAccessExpressionSyntax access => invocation.WithExpression (access.WithName (SyntaxFactory.IdentifierName (asyncName).WithTriviaFrom (access.Name))),
                    IdentifierNameSyntax name => invocation.WithExpression (SyntaxFactory.IdentifierName (asyncName).WithTriviaFrom (name)),
                    MemberBindingExpressionSyntax => null,   // dialog?.ShowDialog (): the awaited form is not a drop-in
                    _ => null,
                };

                return renamed is null ? null : new Replacement ($"Await {asyncName}", invocation, renamed);
            }
            case BrowserBlockingCallAnalyzer.SyncOverAsyncId: {
                // task.Result
                if (node.FirstAncestorOrSelf<MemberAccessExpressionSyntax> () is { Name.Identifier.ValueText: "Result" } result
                    && result.Parent is not InvocationExpressionSyntax)
                    return new Replacement ("Await the task", result, result.Expression);

                if (node.FirstAncestorOrSelf<InvocationExpressionSyntax> () is not { Expression: MemberAccessExpressionSyntax call } invocation)
                    return null;

                // task.Wait () -- not Wait (timeout), which answers whether the task finished in time.
                if (call.Name.Identifier.ValueText == "Wait" && invocation.ArgumentList.Arguments.Count == 0 && invocation.Parent is ExpressionStatementSyntax)
                    return new Replacement ("Await the task", invocation, call.Expression);

                // task.GetAwaiter ().GetResult ()
                if (call.Name.Identifier.ValueText == "GetResult"
                    && call.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetAwaiter" } getAwaiter, ArgumentList.Arguments.Count: 0 })
                    return new Replacement ("Await the task", invocation, getAwaiter.Expression);

                return null;   // Task.WaitAll/WaitAny: the awaited forms (WhenAll/WhenAny) answer differently.
            }
            case BrowserBlockingCallAnalyzer.ThreadSleepId: {
                if (node.FirstAncestorOrSelf<InvocationExpressionSyntax> () is not { ArgumentList.Arguments.Count: 1 } sleep)
                    return null;

                var delay = SyntaxFactory.InvocationExpression (
                    SyntaxFactory.ParseExpression ("global::System.Threading.Tasks.Task.Delay")
                        .WithAdditionalAnnotations (Simplifier.Annotation)
                        .WithTrailingTrivia (sleep.Expression.GetTrailingTrivia ()),   // "Sleep (n)" stays "Delay (n)"
                    sleep.ArgumentList);

                return new Replacement ("Await Task.Delay", sleep, delay);
            }
            default:
                return null;
            }
        }

        /// <summary>
        /// Whether an <c>await</c> can go where <paramref name="target"/> is without changing anything the
        /// caller can see; <paramref name="function"/> is the enclosing function to mark <c>async</c>, or
        /// null when it already is.
        /// </summary>
        private static bool CanAwaitAt (SyntaxNode target, SemanticModel model, CancellationToken token, out SyntaxNode? function)
        {
            function = null;

            for (var node = target.Parent; node is not null; node = node.Parent) {
                switch (node) {
                case LockStatementSyntax:
                case QueryExpressionSyntax:
                    return false;   // await is not allowed there
                case AnonymousFunctionExpressionSyntax lambda:
                    if (lambda.AsyncKeyword.IsKind (SyntaxKind.AsyncKeyword))
                        return true;
                    function = lambda;
                    return model.GetSymbolInfo (lambda, token).Symbol is IMethodSymbol symbol && IsEventHandlerShape (symbol);
                case LocalFunctionStatementSyntax local:
                    if (local.Modifiers.Any (SyntaxKind.AsyncKeyword))
                        return true;
                    function = local;
                    return model.GetDeclaredSymbol (local, token) is IMethodSymbol localSymbol && IsEventHandlerShape (localSymbol);
                case MethodDeclarationSyntax method:
                    if (method.Modifiers.Any (SyntaxKind.AsyncKeyword))
                        return true;
                    function = method;
                    return model.GetDeclaredSymbol (method, token) is IMethodSymbol methodSymbol && IsEventHandlerShape (methodSymbol);
                case BaseMethodDeclarationSyntax:
                case AccessorDeclarationSyntax:
                case GlobalStatementSyntax:
                case FieldDeclarationSyntax:
                    return false;   // constructors, operators, properties: none can be async
                }
            }

            return false;
        }

        // void Handler (object sender, SomeEventArgs e): the one shape where async void is the idiom.
        private static bool IsEventHandlerShape (IMethodSymbol method)
        {
            if (!method.ReturnsVoid || method.Parameters.Length != 2)
                return false;

            for (var t = method.Parameters[1].Type as INamedTypeSymbol; t is not null; t = t.BaseType)
                if (t.Name == "EventArgs" && t.ContainingNamespace?.ToDisplayString () == "System")
                    return true;

            return false;
        }

        private static async Task<Document> ApplyAsync (Document document, ExpressionSyntax target, ExpressionSyntax awaited,
            SyntaxNode? function, CancellationToken token)
        {
            var root = await document.GetSyntaxRootAsync (token).ConfigureAwait (false);

            if (root is null)
                return document;

            // The call's own formatting is kept as written (no Formatter pass): only the await is new.
            var awaitExpression = (ExpressionSyntax) SyntaxFactory.AwaitExpression (
                    SyntaxFactory.Token (SyntaxKind.AwaitKeyword).WithTrailingTrivia (SyntaxFactory.Space), awaited.WithoutTrivia ())
                .WithTriviaFrom (target);

            // (await x).Member: await binds looser than member access.
            if (NeedsParentheses (target))
                awaitExpression = SyntaxFactory.ParenthesizedExpression (awaitExpression.WithoutTrivia ()).WithTriviaFrom (target);

            if (function is null)
                return document.WithSyntaxRoot (root.ReplaceNode (target, awaitExpression));

            root = root.TrackNodes (target, function);
            root = root.ReplaceNode (root.GetCurrentNode (target)!, awaitExpression);

            var current = root.GetCurrentNode (function)!;

            return document.WithSyntaxRoot (root.ReplaceNode (current, MakeAsync (current)));
        }

        private static bool NeedsParentheses (ExpressionSyntax target) => target.Parent switch {
            MemberAccessExpressionSyntax access => access.Expression == target,
            ElementAccessExpressionSyntax element => element.Expression == target,
            ConditionalAccessExpressionSyntax conditional => conditional.Expression == target,
            InvocationExpressionSyntax invocation => invocation.Expression == target,
            PostfixUnaryExpressionSyntax => true,
            _ => false,
        };

        private static SyntaxNode MakeAsync (SyntaxNode function)
        {
            var asyncToken = SyntaxFactory.Token (SyntaxKind.AsyncKeyword).WithTrailingTrivia (SyntaxFactory.Space);

            switch (function) {
            case AnonymousFunctionExpressionSyntax lambda:
                return lambda.WithoutLeadingTrivia ().WithAsyncKeyword (asyncToken).WithLeadingTrivia (lambda.GetLeadingTrivia ());
            case MethodDeclarationSyntax method:
                return method.Modifiers.Count > 0
                    ? method.AddModifiers (asyncToken)
                    : method.WithReturnType (method.ReturnType.WithoutLeadingTrivia ())
                        .WithModifiers (SyntaxFactory.TokenList (asyncToken.WithLeadingTrivia (method.ReturnType.GetLeadingTrivia ())));
            case LocalFunctionStatementSyntax local:
                return local.Modifiers.Count > 0
                    ? local.AddModifiers (asyncToken)
                    : local.WithReturnType (local.ReturnType.WithoutLeadingTrivia ())
                        .WithModifiers (SyntaxFactory.TokenList (asyncToken.WithLeadingTrivia (local.ReturnType.GetLeadingTrivia ())));
            default:
                return function;
            }
        }
    }
}
