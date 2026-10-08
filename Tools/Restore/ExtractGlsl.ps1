# ExtractGlsl.ps1 - lấy mã GLSL thật của shader game gốc (OpenGL ES 3) từ bản export Yaml của AssetRipper.
#
# AssetRipper chế độ Dummy chỉ cho shader giả (còn thuộc tính, mất ruột). Chế độ Yaml giữ lại các chương trình đã biên dịch
# dưới dạng khối nén LZ4 (compressedBlob + offsets / compressedLengths / decompressedLengths) trong file .asset của shader.
# Script giải nén từng khối và lấy ra mọi đoạn mã bắt đầu bằng '#' (các biến thể vertex/fragment), mỗi shader một file .glsl.
#
# Cách chạy (PowerShell):
#   powershell -ExecutionPolicy Bypass -File Tools\Restore\ExtractGlsl.ps1 `
#     -ShaderDir "G:\Game_2D\APK\Export_1.15_ShadersYaml\ExportedProject\Assets\Shader" `
#     -OutDir    "G:\Game_2D\APK\ShaderGLSL"
#
# Kết quả: mã đã biên dịch (tên biến kiểu u_xlat0, không ghi chú) - đọc để hiểu công thức rồi viết lại thành shader Unity,
# không chép thẳng. Chỉ có các shader nằm trong bản build.

param([string]$ShaderDir, [string]$OutDir)

Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Text;
public static class Lz4 {
  public static byte[] Decode(byte[] src, int outLen) {
    var dst = new byte[outLen]; int s = 0, d = 0;
    while (s < src.Length) {
      int token = src[s++]; int lit = token >> 4;
      if (lit == 15) { int b; do { b = src[s++]; lit += b; } while (b == 255); }
      Array.Copy(src, s, dst, d, lit); s += lit; d += lit;
      if (s >= src.Length) break;
      int off = src[s] | (src[s+1] << 8); s += 2;
      int ml = token & 15;
      if (ml == 15) { int b; do { b = src[s++]; ml += b; } while (b == 255); }
      ml += 4; int m = d - off;
      for (int i = 0; i < ml; i++) dst[d++] = dst[m++];
    }
    return dst;
  }
  public static List<string> Strings(byte[] b) {
    var res = new List<string>();
    for (int i = 0; i + 8 < b.Length; i++) {
      int len = BitConverter.ToInt32(b, i);
      if (len < 64 || len > 400000 || i + 4 + len > b.Length) continue;
      if (b[i+4] != (byte)'#') continue;
      bool ok = true;
      for (int k = 0; k < len; k++) { byte c = b[i+4+k]; if (c < 9 || c > 126) { ok = false; break; } }
      if (!ok) continue;
      res.Add(Encoding.ASCII.GetString(b, i + 4, len)); i += 3 + len;
    }
    return res;
  }
}
"@

function HexBytes([string]$h) { $n = $h.Length / 2; $a = New-Object byte[] $n; for ($i = 0; $i -lt $n; $i++) { $a[$i] = [Convert]::ToByte($h.Substring($i*2, 2), 16) }; $a }
function LeInt([string]$h) { [BitConverter]::ToUInt32((HexBytes $h), 0) }

New-Item -ItemType Directory -Force $OutDir | Out-Null
foreach ($f in Get-ChildItem $ShaderDir -Filter *.asset) {
  $lines = Get-Content $f.FullName
  $get = { param($key) $idx = [Array]::FindIndex($lines, [Predicate[string]]{ param($l) $l -match "^\s\s$key" }); $idx }
  $iOff = & $get 'offsets:'; $iC = & $get 'compressedLengths:'; $iD = & $get 'decompressedLengths:'; $iB = & $get 'compressedBlob:'
  if ($iB -lt 0) { continue }
  $blobHex = ($lines[$iB] -replace '^\s*compressedBlob:\s*', '').Trim()
  if ($blobHex.Length -lt 8) { continue }
  $blob = HexBytes $blobHex
  $offs = @(); for ($i = $iOff + 1; $i -lt $iC; $i++) { $offs += LeInt (($lines[$i] -replace '^\s*-\s*', '').Trim()) }
  $cl = @(); for ($i = $iC + 1; $i -lt $iD; $i++) { $cl += LeInt (($lines[$i] -replace '^\s*-\s*', '').Trim()) }
  $dl = @(); for ($i = $iD + 1; $i -lt $iB; $i++) { $dl += LeInt (($lines[$i] -replace '^\s*-\s*', '').Trim()) }
  $all = New-Object System.Collections.Generic.List[string]
  for ($p = 0; $p -lt $offs.Count; $p++) {
    $chunk = New-Object byte[] $cl[$p]; [Array]::Copy($blob, [int]$offs[$p], $chunk, 0, [int]$cl[$p])
    $raw = [Lz4]::Decode($chunk, [int]$dl[$p])
    foreach ($s in [Lz4]::Strings($raw)) { if (-not $all.Contains($s)) { $all.Add($s) } }
  }
  $name = $f.BaseName
  $sb = New-Object System.Text.StringBuilder
  $n = 0; foreach ($s in $all) { $n++; [void]$sb.AppendLine("//////// PROGRAM $n ////////"); [void]$sb.AppendLine($s) }
  [IO.File]::WriteAllText((Join-Path $OutDir "$name.glsl"), $sb.ToString())
  "{0,-55} programs={1}" -f $name, $all.Count
}
