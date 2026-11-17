param(
    [string]$Godot = "godot",
    [string]$Project = "game/SeedField.Game",
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$GameArguments
)

# Godot's host ignores the project runtimeconfig for GC. The controller benchmark
# and the release acceptance path both require Server GC.
$env:DOTNET_gcServer = "1"
& $Godot --path $Project @GameArguments
exit $LASTEXITCODE
