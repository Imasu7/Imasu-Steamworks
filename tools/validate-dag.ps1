# validate-dag.ps1 -- validate agent DAG config + model IDs + time-based routing
# Usage:
#   powershell -File tools/validate-dag.ps1              # config + model names
#   powershell -File tools/validate-dag.ps1 -Probe       # also ping each text model
#   powershell -File tools/validate-dag.ps1 -Probe -IncludeVision

param(
    [switch]$Probe,
    [switch]$IncludeVision
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$agentDir = Join-Path $repoRoot ".opencode\agent"
$configPath = Join-Path $repoRoot ".opencode\opencode.json"

$failures = 0
$warnings = 0

function Write-Ok($msg)   { Write-Host "  [OK]    $msg" -ForegroundColor Green }
function Write-Fail($msg) { Write-Host "  [FAIL]  $msg" -ForegroundColor Red; $script:failures++ }
function Write-Warn($msg) { Write-Host "  [WARN]  $msg" -ForegroundColor Yellow; $script:warnings++ }
function Write-Info($msg) { Write-Host "  [INFO]  $msg" -ForegroundColor Cyan }

# -- Expected agents --
$expectedAgents = @("dag", "scout", "scribe", "hand-free", "depth-free", "hand", "depth", "prover", "eyes")

# -- Expected model IDs --
$expectedModels = @{
    "dag"        = "opencode/longcat-2.5-preview-free"
    "scout"      = "opencode/space-bunny-free"
    "scribe"     = "opencode/space-bunny-free"
    "hand-free"  = "opencode/mimo-v2.6-flash-free"
    "depth-free" = "opencode/big-pickle"
    "hand"       = "omniroute/opencode-go/deepseek-v4.1-flash"
    "depth"      = "omniroute/opencode-go/deepseek-v4.1-flash"
    "prover"     = "omniroute/opencode-go/deepseek-v4.1-flash"
    "eyes"       = "omniroute/opencode-go/deepseek-v4-flash-vision-exp"
}

# -- Known-blocked models (probe-verified, cannot run as subagents) --
# These are catalogue-active but fail at dispatch with
# "OpenCode's free tier can only be used from within OpenCode".
# Binding one to a node is a hard config error, not a runtime surprise.
$blockedModels = @(
    "opencode/longcat-2.5-preview-free"
)

# -- Peak hours (Mauritius UTC+4) --
$peakStartMUT = 10  # 10:00 MUT
$peakEndMUT   = 14  # 14:00 MUT

Write-Host ""
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host "  DAG validator" -ForegroundColor Cyan
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""

# -- 1. Config file --
Write-Host "[1] opencode.json" -ForegroundColor Yellow
if (-not (Test-Path $configPath)) {
    Write-Fail "opencode.json not found at $configPath"
} else {
    Write-Ok "opencode.json exists"
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    if ($config.default_agent -ne "dag") {
        Write-Fail "default_agent should be 'dag', got '$($config.default_agent)'"
    } else {
        Write-Ok "default_agent = dag"
    }
    if ($config.subagent_depth -ne 1) {
        Write-Warn "subagent_depth should be 1, got '$($config.subagent_depth)'"
    } else {
        Write-Ok "subagent_depth = 1"
    }
}
Write-Host ""

# -- 2. Agent files exist --
Write-Host "[2] Agent files" -ForegroundColor Yellow
foreach ($agent in $expectedAgents) {
    $path = Join-Path $agentDir "$agent.md"
    if (-not (Test-Path $path)) {
        Write-Fail "$agent.md not found"
    } else {
        Write-Ok "$agent.md exists"
    }
}
Write-Host ""

# -- 3. Model IDs in frontmatter --
Write-Host "[3] Model IDs in frontmatter" -ForegroundColor Yellow
foreach ($agent in $expectedAgents) {
    $path = Join-Path $agentDir "$agent.md"
    if (-not (Test-Path $path)) { continue }
    $content = Get-Content $path -Raw
    $expectedModel = $expectedModels[$agent]
    if ($content -match "(?m)^model:\s*(.+)$") {
        $actualModel = $Matches[1].Trim()
        if ($actualModel -eq $expectedModel) {
            Write-Ok "$agent -> $actualModel"
        } else {
            Write-Fail "$agent model mismatch: expected '$expectedModel', got '$actualModel'"
        }
    } else {
        Write-Fail "$agent has no model field in frontmatter"
    }
}
Write-Host ""

# -- 4. No OmniRoute combos --
Write-Host "[4] No OmniRoute combos" -ForegroundColor Yellow
$comboFound = $false
foreach ($agent in $expectedAgents) {
    $path = Join-Path $agentDir "$agent.md"
    if (-not (Test-Path $path)) { continue }
    $content = Get-Content $path -Raw
    if ($content -match "auto/") {
        Write-Fail "$agent contains OmniRoute combo reference (auto/)"
        $comboFound = $true
    }
}
if (-not $comboFound) {
    Write-Ok "No OmniRoute combos found"
}
Write-Host ""

# -- 5. No 1x/2x cost tier references --
Write-Host "[5] No 1x/2x cost tier references" -ForegroundColor Yellow
$tierFound = $false
foreach ($agent in $expectedAgents) {
    $path = Join-Path $agentDir "$agent.md"
    if (-not (Test-Path $path)) { continue }
    $content = Get-Content $path -Raw
    if ($content -match "\b[12]x\b") {
        Write-Warn "$agent contains 1x/2x reference (should be removed)"
        $tierFound = $true
    }
}
if (-not $tierFound) {
    Write-Ok "No 1x/2x references found"
}
Write-Host ""

# -- 6. No known-blocked models assigned to subagent-mode agents --
Write-Host "[6] No known-blocked models assigned to subagents" -ForegroundColor Yellow
$blockedHit = $false
foreach ($agent in $expectedAgents) {
    $path = Join-Path $agentDir "$agent.md"
    if (-not (Test-Path $path)) { continue }
    $content = Get-Content $path -Raw
    if ($content -notmatch "(?m)^model:\s*(.+)$") { continue }
    # Skip primary-mode agents (they run as the main agent, not a subagent)
    if ($content -match "(?m)^mode:\s*primary\s*$") { continue }
    $actualModel = $Matches[1].Trim()
    if ($blockedModels -contains $actualModel) {
        Write-Fail "$agent is bound to BLOCKED model '$actualModel' (cannot run as subagent)"
        $blockedHit = $true
    }
}
if (-not $blockedHit) {
    Write-Ok "No blocked models assigned to subagents"
}
Write-Host ""

# -- 7. Time-based routing check --
Write-Host "[7] Time-based routing (peak hours)" -ForegroundColor Yellow
$now = [DateTime]::UtcNow
$mutTime = $now.AddHours(4)
$mutHour = $mutTime.Hour
$isPeak = ($mutHour -ge $peakStartMUT -and $mutHour -lt $peakEndMUT)

Write-Info "Current UTC:  $($now.ToString('yyyy-MM-dd HH:mm'))"
Write-Info "Current MUT:  $($mutTime.ToString('yyyy-MM-dd HH:mm'))"
if ($isPeak) {
    Write-Warn "PEAK HOURS - Deepseek workers should NOT be used"
    Write-Warn "Free workers only: hand-free, depth-free, scout, scribe"
} else {
    Write-Ok "Off-peak - full DAG available"
}
Write-Host ""

# -- 8. Model probe (optional) --
if ($Probe) {
    Write-Host "[8] Model probe" -ForegroundColor Yellow
    $modelsToProbe = @(
        "opencode/longcat-2.5-preview-free",
        "opencode/space-bunny-free",
        "opencode/mimo-v2.6-flash-free",
        "opencode/big-pickle"
    )
    if ($IncludeVision) {
        $modelsToProbe += "omniroute/opencode-go/deepseek-v4-flash-vision-exp"
    }
    foreach ($modelId in $modelsToProbe) {
        Write-Info "Probing $modelId ..."
        $result = & opencode models --json 2>$null | ConvertFrom-Json
        $found = $false
        foreach ($provider in $result.providers) {
            foreach ($m in $provider.models) {
                if ($m.id -eq $modelId) {
                    $found = $true
                    break
                }
            }
            if ($found) { break }
        }
        if ($found) {
            Write-Ok "$modelId found in catalogue"
        } else {
            Write-Fail "$modelId NOT found in catalogue"
        }
    }
    Write-Host ""
}

# -- Summary --
Write-Host "=======================================================" -ForegroundColor Cyan
if ($failures -eq 0 -and $warnings -eq 0) {
    Write-Host "  PASS - all checks passed" -ForegroundColor Green
} elseif ($failures -eq 0) {
    Write-Host "  PASS with warnings ($warnings)" -ForegroundColor Yellow
} else {
    Write-Host "  FAIL - $failures failure(s), $warnings warning(s)" -ForegroundColor Red
}
Write-Host "=======================================================" -ForegroundColor Cyan
Write-Host ""

exit $failures
