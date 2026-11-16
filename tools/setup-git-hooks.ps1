# Enables the repository's commit-msg hook for this clone.
$ErrorActionPreference = "Stop"

git config core.hooksPath .githooks
Write-Host "core.hooksPath = .githooks"
Write-Host "Commit subjects must match: <type>(<scope>): <summary>"
