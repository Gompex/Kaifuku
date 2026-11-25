# Publishes a Kaifuku version into Gompex's main Dalamud repo (Gompex/DalamudPlugins, repo.json),
# the same way as the other plugins: release tag "Kaifuku-v<version>" with "Kaifuku-<version>.0.zip" attached,
# plus the Kaifuku entry in repo.json. It never touches README.md.
# Usage:  powershell -ExecutionPolicy Bypass -File C:\Dev\Kaifuku\tools\publish.ps1 -Version 2.2.0
param([string]$Version = '2.1.0')

$owner    = 'Gompex'
$mainRepo = 'Gompex/DalamudPlugins'
$asmVer   = "$Version.0"
$root     = 'C:\Dev\Kaifuku'
$proj     = 'C:\Dev\Kaifuku\Kaifuku'
$repoDir  = 'C:\Dev\DalamudPlugins'
$repoUrl  = "https://raw.githubusercontent.com/$mainRepo/main/repo.json"
$jp       = [string][char]0x56DE + [char]0x5FA9

if (-not (Test-Path "$proj\Kaifuku.csproj")) { throw "Kaifuku.csproj not found in $proj" }

# 1. Version + build (stop on failure)
Set-Location $proj
$csproj = [IO.File]::ReadAllText("$proj\Kaifuku.csproj") -replace '<Version>[^<]+</Version>', "<Version>$asmVer</Version>"
[IO.File]::WriteAllText("$proj\Kaifuku.csproj", $csproj)
Remove-Item bin, obj -Recurse -Force -ErrorAction SilentlyContinue
dotnet build -c Release
if ($LASTEXITCODE -ne 0) { throw "Build failed - nothing was published." }
$latest = (Get-ChildItem bin -Recurse -Filter latest.zip | Select-Object -First 1).FullName
if (-not $latest) { throw "latest.zip not found under bin." }
$asset = Join-Path $env:TEMP "Kaifuku-$asmVer.zip"
Copy-Item $latest $asset -Force

# 2. Release in the main repo
$tag = "Kaifuku-v$Version"
gh release view $tag --repo $mainRepo 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { gh release upload $tag $asset --repo $mainRepo --clobber }
else {
    gh release create $tag $asset --repo $mainRepo --title "Kaifuku $Version" --notes "Kaifuku ${Version}: click-to-heal party frames."
    if ($LASTEXITCODE -ne 0) { throw "Could not create the release in $mainRepo." }
}
$dl = "https://github.com/$mainRepo/releases/download/$tag/Kaifuku-$asmVer.zip"

# 3. repo.json entry (other plugins untouched)
if (-not (Test-Path "$repoDir\.git")) { gh repo clone $mainRepo $repoDir }
Set-Location $repoDir
git pull --ff-only
$list = @(Get-Content "$repoDir\repo.json" -Raw | ConvertFrom-Json)
$list = @($list | Where-Object { $_ -and $_.InternalName -ne 'Kaifuku' -and $_.InternalName -ne 'HealFrames' })
$entry = [ordered]@{
    Author = 'Dawid'; Name = 'Kaifuku'; InternalName = 'Kaifuku'; AssemblyVersion = $asmVer
    Description = 'HealBot-style click-to-heal party frames: click a member to heal them, with Shift, Ctrl, Alt and mouse buttons 4/5 for more skills. Skins, a binding guide, alliance frames, profiles, and healer awareness: raises on the way, removable debuffs, heal-to-full debuffs, tank busters, raidwides, aggro, cast bars and pets. One click, one action.'
    ApplicableVersion = 'any'; RepoUrl = "https://github.com/$owner/Kaifuku"
    Tags = @('healer', 'party', 'frames', 'combat', 'ui'); DalamudApiLevel = 15
    LoadRequiredState = 0; LoadSync = $false; CanUnloadAsync = $false; LoadPriority = 0
    Punchline = "Kaifuku ($jp, recovery): click-to-heal party frames."; AcceptsFeedback = $false
    DownloadLinkInstall = $dl; DownloadLinkUpdate = $dl; DownloadLinkTesting = $dl
    IsHide = $false; IsTestingExclusive = $false
    LastUpdate = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds(); DownloadCount = 0
}
$list += [pscustomobject]$entry
[IO.File]::WriteAllText("$repoDir\repo.json", (ConvertTo-Json -InputObject $list -Depth 5))
git add repo.json; git commit -m "Kaifuku $Version" | Out-Null; git push

# 4. Commit the version bump in the code repo
Set-Location $root
git add Kaifuku/Kaifuku.csproj; git commit -m "Kaifuku $Version" | Out-Null; git push
"Done: Kaifuku $asmVer is listed in $repoUrl"
