param([string]$Out)
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot "plan_data.ps1")

function Esc([string]$s) { [System.Security.SecurityElement]::Escape($s) }
function Cell([string]$ref, $value, [int]$style) {
  if ($null -eq $value -or "$value" -eq '') { return "<c r=`"$ref`" s=`"$style`"/>" }
  if ($value -is [int]) { return "<c r=`"$ref`" s=`"$style`"><v>$value</v></c>" }
  return "<c r=`"$ref`" s=`"$style`" t=`"inlineStr`"><is><t xml:space=`"preserve`">$(Esc $value)</t></is></c>"
}

$rows = New-Object System.Text.StringBuilder
$merges = @()
$headers = @('Sub Task', 'Nhóm', 'Thời gian (giờ)', 'Mô tả chi tiết', 'Main Task', 'Tiến độ (%)', 'Ngày hoàn thành')
$cols = 'A','B','C','D','E','F','G'
[void]$rows.Append('<row r="1" ht="22" customHeight="1">')
for ($i = 0; $i -lt 7; $i++) { [void]$rows.Append((Cell "$($cols[$i])1" $headers[$i] 1)) }
[void]$rows.Append('</row>')

$r = 2
$mainIndex = 0
foreach ($group in $groups) {
  foreach ($main in $group.mains) {
    # Alternate a light fill per Main Task so blocks read at a glance (no merged cells:
    # Google Sheets import keeps row lines inside merges, which hid the grouping).
    $o = if ($mainIndex % 2 -eq 1) { 4 } else { 0 }
    # PowerShell flattens @( @(...) ) when a Main Task has a single row: wrap it back.
    $list = $main.rows
    if ($list.Count -gt 0 -and $list[0] -isnot [array]) { $list = ,$list }
    foreach ($row in $list) {
      [void]$rows.Append("<row r=`"$r`">")
      [void]$rows.Append((Cell "A$r" $row[0] (2 + $o)))
      [void]$rows.Append((Cell "B$r" $group.g (3 + $o)))
      [void]$rows.Append((Cell "C$r" '' (4 + $o)))
      [void]$rows.Append((Cell "D$r" $row[1] (2 + $o)))
      [void]$rows.Append((Cell "E$r" $main.m (3 + $o)))
      [void]$rows.Append((Cell "F$r" $row[2] (4 + $o)))
      [void]$rows.Append((Cell "G$r" $row[3] (4 + $o)))
      [void]$rows.Append('</row>')
      $r++
    }
    $mainIndex++
  }
}
$last = $r - 1
# Overall progress (average of the column), as a formula.
[void]$rows.Append("<row r=`"$r`"><c r=`"A$r`" s=`"1`" t=`"inlineStr`"><is><t>TIẾN ĐỘ CHUNG</t></is></c>")
[void]$rows.Append("<c r=`"B$r`" s=`"1`"/><c r=`"C$r`" s=`"1`"/><c r=`"D$r`" s=`"1`"/><c r=`"E$r`" s=`"1`"/><c r=`"F$r`" s=`"5`"><f>ROUND(AVERAGE(F2:F$last),0)</f></c><c r=`"G$r`" s=`"1`"/></row>")

$mergeXml = if ($merges.Count) { "<mergeCells count=`"$($merges.Count)`">" + (($merges | ForEach-Object { "<mergeCell ref=`"$_`"/>" }) -join '') + '</mergeCells>' } else { '' }

$sheet = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>
<cols><col min="1" max="1" width="34" customWidth="1"/><col min="2" max="2" width="26" customWidth="1"/><col min="3" max="3" width="13" customWidth="1"/><col min="4" max="4" width="88" customWidth="1"/><col min="5" max="5" width="30" customWidth="1"/><col min="6" max="6" width="12" customWidth="1"/><col min="7" max="7" width="17" customWidth="1"/></cols>
<sheetData>$($rows.ToString())</sheetData>
$mergeXml
</worksheet>
"@

$styles = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
<fonts count="2"><font><sz val="10"/><name val="Arial"/></font><font><b/><sz val="10"/><name val="Arial"/></font></fonts>
<fills count="4"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FFDDEBF7"/><bgColor indexed="64"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFF2F2F2"/><bgColor indexed="64"/></patternFill></fill></fills>
<borders count="2"><border><left/><right/><top/><bottom/><diagonal/></border><border><left style="thin"><color rgb="FF999999"/></left><right style="thin"><color rgb="FF999999"/></right><top style="thin"><color rgb="FF999999"/></top><bottom style="thin"><color rgb="FF999999"/></bottom><diagonal/></border></borders>
<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
<cellXfs count="9">
<xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
<xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center" wrapText="1"/></xf>
<xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
<xf numFmtId="0" fontId="1" fillId="0" borderId="1" xfId="0" applyFont="1" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
<xf numFmtId="0" fontId="0" fillId="0" borderId="1" xfId="0" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
<xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
<xf numFmtId="0" fontId="0" fillId="3" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
<xf numFmtId="0" fontId="1" fillId="3" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1"><alignment vertical="center" wrapText="1"/></xf>
<xf numFmtId="0" fontId="0" fillId="3" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1"><alignment horizontal="center" vertical="center"/></xf>
</cellXfs>
<cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
</styleSheet>
"@

$files = @{
  '[Content_Types].xml' = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>'
  '_rels/.rels' = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>'
  'xl/workbook.xml' = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="Tiến độ" sheetId="1" r:id="rId1"/></sheets><calcPr calcId="191029" fullCalcOnLoad="1"/></workbook>'
  'xl/_rels/workbook.xml.rels' = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>'
  'xl/worksheets/sheet1.xml' = $sheet
  'xl/styles.xml' = $styles
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $Out) { Remove-Item $Out -Force }
$zip = [System.IO.Compression.ZipFile]::Open($Out, 'Create')
$utf8 = New-Object System.Text.UTF8Encoding($false)
foreach ($name in $files.Keys) {
  $entry = $zip.CreateEntry($name)
  $stream = $entry.Open()
  $bytes = $utf8.GetBytes($files[$name])
  $stream.Write($bytes, 0, $bytes.Length)
  $stream.Close()
}
$zip.Dispose()
"wrote $Out rows=$last merges=$($merges.Count)"
