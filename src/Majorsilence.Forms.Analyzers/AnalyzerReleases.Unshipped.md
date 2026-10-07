; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
MFB001 | Usage | Warning | BrowserBlockingCallAnalyzer: blocking modal call (ShowDialog, MessageBox.Show, ...) on the browser target
MFB002 | Usage | Warning | BrowserBlockingCallAnalyzer: synchronous wait on a task (.Result, .Wait (), GetResult ()) on the browser target
MFB003 | Usage | Warning | BrowserBlockingCallAnalyzer: Thread.Sleep on the browser target
