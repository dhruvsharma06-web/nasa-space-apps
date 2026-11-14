<#
.SYNOPSIS
  Publish the Unity WebGL build to the gh-pages branch (GitHub Pages). Windows PowerShell 5.1+.

.DESCRIPTION
  scripts\publish_web.ps1 [-BuildDir Builds\WebGL] [-Push]

  -BuildDir  Unity WebGL output folder (default: Builds\WebGL). Build it UNCOMPRESSED
             (Player > Publishing Settings > Compression Format = Disabled): GitHub Pages
             cannot send the Content-Encoding headers that .br/.gz builds need.
  -Push      Push gh-pages to origin. Only use this after the team lead says "approved".
             Without it, the script only commits to the local gh-pages branch.

  Uses a temporary git worktree, so your current branch and working tree are never touched,
  and nothing is ever written to main.
#>
[CmdletBinding()]
param(
  [string]$BuildDir = "Builds\WebGL",
  [switch]$Push
)
$ErrorActionPreference = "Stop"

function Invoke-Git {
  # Run git, fail on a non-zero exit code. Returns trimmed stdout.
  $ErrorActionPreference = "Continue"   # see Test-Git
  $out = & git.exe @args
  if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed (exit $LASTEXITCODE)" }
  if ($out) { ($out | Out-String).Trim() } else { "" }
}
function Test-Git {
  # Run git quietly and return $true on exit code 0. Function-local "Continue" so that
  # Windows PowerShell 5.1 doesn't turn git's stderr output into a terminating error.
  $ErrorActionPreference = "Continue"
  & git.exe @args 2>$null | Out-Null
  return ($LASTEXITCODE -eq 0)
}

$root = Invoke-Git rev-parse --show-toplevel
Set-Location $root
$template = Join-Path $root "scripts\publish_web.landing.html"
if (-not (Test-Path $BuildDir)) { throw "Build folder not found: $BuildDir" }
$build = (Resolve-Path $BuildDir).Path

# ---- Check the build -------------------------------------------------------------------
if (-not (Test-Path (Join-Path $build "index.html"))) { throw "No index.html in ${build}: is this a Unity WebGL build?" }
if (-not (Test-Path (Join-Path $build "Build"))) { throw "No Build\ folder in ${build}: is this a Unity WebGL build?" }
$compressed = Get-ChildItem (Join-Path $build "Build") -Recurse -File | Where-Object { $_.Extension -in ".br", ".gz" }
if ($compressed) {
  throw "Compressed build files (.br/.gz) found. GitHub Pages can't serve them. Rebuild with Compression Format = Disabled (or enable Decompression Fallback)."
}
$big = Get-ChildItem $build -Recurse -File | Where-Object { $_.Length -gt 95MB }
if ($big) { throw "Files over 95 MB (GitHub's limit is 100 MB per file): $($big.FullName -join ', ')" }
if (-not (Test-Path $template)) { throw "Missing landing template: $template" }

# ---- Links for the landing page ---------------------------------------------------------
$origin = Invoke-Git remote get-url origin
$repoUrl = ($origin -replace '^git@github\.com:', 'https://github.com/') -replace '\.git$', ''
$apkUrl = if ($env:APK_URL) { $env:APK_URL } else { "$repoUrl/releases/latest" }
$commit = Invoke-Git rev-parse --short HEAD
$buildDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
$ownerRepo = $repoUrl -replace '^https://github\.com/', ''
$owner, $repo = $ownerRepo.Split('/', 2)
$pagesUrl = "https://$owner.github.io/$repo/"

# ---- Worktree on gh-pages ---------------------------------------------------------------
$wt = Join-Path ([IO.Path]::GetTempPath()) ("astra-pages-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
try {
  Test-Git fetch --quiet origin gh-pages | Out-Null   # fine if gh-pages doesn't exist yet
  if (Test-Git show-ref --verify --quiet refs/remotes/origin/gh-pages) {
    Invoke-Git worktree add --quiet -B gh-pages $wt origin/gh-pages | Out-Null
  } elseif (Test-Git show-ref --verify --quiet refs/heads/gh-pages) {
    Invoke-Git worktree add --quiet $wt gh-pages | Out-Null
  } else {
    Invoke-Git worktree add --quiet --no-checkout --detach $wt HEAD | Out-Null
    Invoke-Git -C $wt checkout --quiet --orphan gh-pages | Out-Null
    Test-Git -C $wt rm -rq --cached . | Out-Null
  }

  # Replace everything on gh-pages with the new build.
  Get-ChildItem $wt -Force | Where-Object { $_.Name -ne ".git" } | Remove-Item -Recurse -Force
  $play = Join-Path $wt "play"
  New-Item -ItemType Directory -Path $play | Out-Null
  Copy-Item -Path (Join-Path $build "*") -Destination $play -Recurse -Force

  $html = [IO.File]::ReadAllText($template)
  $html = $html.Replace("{{REPO_URL}}", $repoUrl).Replace("{{APK_URL}}", $apkUrl).Replace("{{COMMIT}}", $commit).Replace("{{BUILD_DATE}}", $buildDate)
  $utf8 = New-Object System.Text.UTF8Encoding($false)   # no BOM
  [IO.File]::WriteAllText((Join-Path $wt "index.html"), $html, $utf8)
  [IO.File]::WriteAllText((Join-Path $wt ".nojekyll"), "", $utf8)
  # Plain files only on gh-pages: Pages does not serve Git LFS objects.
  [IO.File]::WriteAllText((Join-Path $wt ".gitattributes"), "* -filter -diff -merge -text`n", $utf8)

  Invoke-Git -C $wt add -A | Out-Null
  if (Test-Git -C $wt diff --cached --quiet) {
    Write-Host "gh-pages already matches this build. Nothing to commit."
  } else {
    Invoke-Git -C $wt commit --quiet -m "Publish web build from $commit" | Out-Null
    Write-Host "Committed to local gh-pages: $(Invoke-Git -C $wt log -1 --format='%h %s')"
  }

  if ($Push) {
    Invoke-Git -C $wt push origin gh-pages | Out-Null
    Write-Host "Pushed. Live in about a minute at: $pagesUrl"
  } else {
    Write-Host "Not pushed. After the team lead says `"approved`", run:"
    Write-Host "  git push origin gh-pages"
    Write-Host "Then the site is at: $pagesUrl"
  }
}
finally {
  Test-Git -C $root worktree remove --force $wt | Out-Null
  if (Test-Path $wt) { Remove-Item $wt -Recurse -Force -ErrorAction SilentlyContinue }
}
