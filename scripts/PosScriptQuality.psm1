Set-StrictMode -Version Latest

<#
Static quality gate for the Support Hub PowerShell surface.

A clean parse is not enough. The defect this gate exists for parsed cleanly:

    $expectedProvider = $privateKey -is [Security.Cryptography.RSACng]
        -and $privateKey.Key.Provider.Provider -eq '...'

PowerShell accepted that as two statements -- an assignment, then a command
named `-and` -- so the provider half never contributed to the result and the
script failed at runtime with "The term '-and' is not recognized". The AST
check below finds that shape anywhere it occurs, and the text checks catch the
formatting that produces it.
#>

# Binary/unary operators that can never legitimately be a command name.
$script:PosOperatorCommandPattern =
    '^-(and|or|not|xor|eq|ne|lt|gt|le|ge|ieq|ine|ilt|igt|ile|ige|ceq|cne|clt|cgt|cle|cge|in|notin|contains|notcontains|icontains|inotcontains|match|notmatch|imatch|inotmatch|cmatch|like|notlike|ilike|inotlike|clike|is|isnot|as|band|bor|bxor|bnot|shl|shr|replace|ireplace|creplace|split|join|f)$'

$script:PosLeadingOperatorPattern =
    '^\s*-(and|or|not|xor|eq|ne|lt|gt|le|ge|in|notin|contains|notcontains|match|notmatch|like|notlike|is|isnot|as|band|bor|bxor|shl|shr|replace|split|join|f)\b'

function New-PosScriptFinding {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][int]$Line,
        [Parameter(Mandatory)][string]$Rule,
        [Parameter(Mandatory)][string]$Message
    )

    return [pscustomobject]@{
        Path = $Path
        Line = $Line
        Rule = $Rule
        Message = $Message
    }
}

<#
.SYNOPSIS
Lists the PowerShell files this gate owns: everything tracked by Git.

.DESCRIPTION
Git tracking is the boundary, so build output, restored packages, and local
publish directories are excluded by construction rather than by an exclusion
list that has to be kept in step with the tree.
#>
function Get-PosTrackedScriptPath {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RepositoryRoot)

    # --others --exclude-standard adds not-yet-committed scripts, so a new file
    # is gated on the run that introduces it rather than the one after.
    $tracked = & git -C $RepositoryRoot ls-files --cached --others --exclude-standard -- '*.ps1' '*.psm1'
    if ($LASTEXITCODE -ne 0) {
        throw "Git could not list the tracked PowerShell files under $RepositoryRoot."
    }

    return @($tracked |
        Sort-Object -Unique |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { Join-Path $RepositoryRoot ($_ -replace '/', '\') } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
}

<#
.SYNOPSIS
Reports whether a line's first code character sits inside a comment or string.
#>
function Test-PosLineStartsInsideToken {
    [CmdletBinding()]
    param(
        [AllowNull()][AllowEmptyCollection()][object[]]$Token,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Line,
        [Parameter(Mandatory)][int]$LineNumber
    )

    $offset = $Line.Length - $Line.TrimStart().Length
    if ($offset -ge $Line.Length) {
        return $false
    }
    $column = $offset + 1

    foreach ($candidate in @($Token)) {
        $extent = $candidate.Extent
        $afterStart = $LineNumber -gt $extent.StartLineNumber `
            -or ($LineNumber -eq $extent.StartLineNumber -and $column -ge $extent.StartColumnNumber)
        $beforeEnd = $LineNumber -lt $extent.EndLineNumber `
            -or ($LineNumber -eq $extent.EndLineNumber -and $column -lt $extent.EndColumnNumber)
        if ($afterStart -and $beforeEnd) {
            return $true
        }
    }

    return $false
}

function Test-PosScriptFile {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $findings = @()

    $tokens = $null
    $parseErrors = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($Path, [ref]$tokens, [ref]$parseErrors)
    foreach ($parseError in @($parseErrors)) {
        $findings += New-PosScriptFinding `
            -Path $Path `
            -Line $parseError.Extent.StartLineNumber `
            -Rule 'ParseError' `
            -Message $parseError.Message
    }
    if ($findings.Count -gt 0) {
        return $findings
    }

    # A command whose name is an operator is always a broken multiline
    # expression, whatever produced it.
    $commands = @($ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.CommandAst] }, $true))
    foreach ($command in $commands) {
        $name = $command.GetCommandName()
        if (-not [string]::IsNullOrWhiteSpace($name) -and $name -match $script:PosOperatorCommandPattern) {
            $findings += New-PosScriptFinding `
                -Path $Path `
                -Line $command.Extent.StartLineNumber `
                -Rule 'OperatorParsedAsCommand' `
                -Message "'$name' is parsed as a command, so the preceding expression is silently truncated. Add a line continuation or keep the expression on one line."
        }
    }

    # Comments and string literals are excluded from the text rules: a doc
    # comment may legitimately quote the broken shape it warns about. Exclusion
    # is decided per line-start position, not per touched line, so a statement
    # that merely *contains* a string is still checked.
    $literalTokens = @($tokens | Where-Object {
        $_.Kind -in @('Comment', 'StringLiteral', 'StringExpandable', 'HereStringLiteral', 'HereStringExpandable')
    })

    $lines = @(Get-Content -LiteralPath $Path)
    for ($index = 0; $index -lt $lines.Count; $index++) {
        if (Test-PosLineStartsInsideToken -Token $literalTokens -Line $lines[$index] -LineNumber ($index + 1)) {
            continue
        }
        $line = $lines[$index]

        if ($line -match '`[ \t]+$') {
            $findings += New-PosScriptFinding `
                -Path $Path `
                -Line ($index + 1) `
                -Rule 'TrailingWhitespaceAfterContinuation' `
                -Message 'A backtick followed by trailing whitespace does not continue the line.'
        }

        if ($index -eq 0 -or $line -notmatch $script:PosLeadingOperatorPattern) {
            continue
        }

        $previous = $lines[$index - 1].TrimEnd()
        if ($previous -notmatch '`$') {
            $findings += New-PosScriptFinding `
                -Path $Path `
                -Line ($index + 1) `
                -Rule 'DanglingOperatorContinuation' `
                -Message 'This line starts with an operator but the previous line has no line continuation.'
        }
    }

    return $findings
}

<#
.SYNOPSIS
Analyses every non-generated PowerShell script and module under a repository.

.OUTPUTS
Zero or more finding objects. An empty result means the surface is clean.
#>
function Invoke-PosScriptQualityAnalysis {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$RepositoryRoot)

    if (-not (Test-Path -LiteralPath $RepositoryRoot -PathType Container)) {
        throw "The repository root to analyse does not exist: $RepositoryRoot"
    }

    $findings = @()
    foreach ($path in Get-PosTrackedScriptPath -RepositoryRoot $RepositoryRoot) {
        $findings += @(Test-PosScriptFile -Path $path)
    }
    return $findings
}

Export-ModuleMember -Function @(
    'Get-PosTrackedScriptPath',
    'Invoke-PosScriptQualityAnalysis',
    'Test-PosLineStartsInsideToken',
    'Test-PosScriptFile'
)
