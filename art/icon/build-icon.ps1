# Rasterizes the app icon SVGs and packs them into a multi-size .ico (needs ImageMagick 7).
# 16 and 24 px use the simplified drawing; 32 px and up use the detailed one.
# Every frame is stored as PNG (supported since Windows Vista), which keeps the file small.
param([string]$Out = (Join-Path $PSScriptRoot '..\..\src\AJ179BatteryTray\Assets\app.ico'))

$ErrorActionPreference = 'Stop'
$sizes = 16, 24, 32, 48, 64, 128, 256
$work = Join-Path ([IO.Path]::GetTempPath()) "aj179-icon-$PID"
New-Item -ItemType Directory -Force $work | Out-Null
try {
    $frames = foreach ($size in $sizes) {
        $svg = Join-Path $PSScriptRoot ($size -le 24 ? 'icon-small.svg' : 'icon-large.svg')
        $png = Join-Path $work "icon-$size.png"
        # Render at 4x density and downsample for clean anti-aliasing at small sizes.
        magick -background none -density (96 * 4 * $size / 256) "RSVG:$svg" `
            -resize "${size}x${size}" -depth 8 -define png:color-type=6 $png
        if ($LASTEXITCODE -ne 0) { throw "magick failed for $size px" }
        [pscustomobject]@{ Size = $size; Data = [IO.File]::ReadAllBytes($png) }
    }

    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dim = [byte]($frame.Size % 256) # 0 means 256
        $writer.Write($dim); $writer.Write($dim); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Data.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Data.Length
    }
    foreach ($frame in $frames) { $writer.Write($frame.Data) }
    $writer.Flush()
    [IO.File]::WriteAllBytes([IO.Path]::GetFullPath($Out), $stream.ToArray())
    magick identify $Out
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force
}
