; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
RCOMP001 | Refit.Composite | Error | Unsupported composite member
RCOMP002 | Refit.Composite | Error | Duplicate API interface in composite
RCOMP003 | Refit.Composite | Error | Type is not accessible to generated code
RCOMP004 | Refit.Composite | Error | Generic composites are not supported
RCOMP005 | Refit.Composite | Error | Invalid message handler
RCOMP006 | Refit.Composite | Warning | API interface declares no Refit methods
