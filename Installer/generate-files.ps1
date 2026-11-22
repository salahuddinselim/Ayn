$app = "C:\Users\User\Desktop\Ayn\Ayn\AynSetup\app"
$out = "C:\Users\User\Desktop\Ayn\Ayn\Installer\Files.wxs"
$dirXml = [System.Collections.Generic.List[string]]::new()
$componentIds = [System.Collections.Generic.List[string]]::new()
$counter = 0

function Emit-Dir($dir, $indent) {
    foreach ($f in (Get-ChildItem -LiteralPath $dir.FullName -File)) {
        $script:counter++
        $id = 'cmp' + $script:counter
        $fid = 'fil' + $script:counter
        $src = $f.FullName.Replace('&', '&amp;').Replace('"', '&quot;')
        $script:dirXml.Add($indent + '<Component Id="' + $id + '" Guid="*">')
        $script:dirXml.Add($indent + '  <File Id="' + $fid + '" Source="' + $src + '" KeyPath="yes" />')
        $script:dirXml.Add($indent + '</Component>')
        $script:componentIds.Add($id)
    }
    foreach ($sub in (Get-ChildItem -LiteralPath $dir.FullName -Directory)) {
        $dirId = 'dir' + $script:counter
        $script:counter++
        $script:dirXml.Add($indent + '<Directory Id="' + $dirId + '" Name="' + $sub.Name + '">')
        Emit-Dir $sub ($indent + '  ')
        $script:dirXml.Add($indent + '</Directory>')
    }
}

Emit-Dir (Get-Item -LiteralPath $app) '    '

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <DirectoryRef Id="APPFOLDER">')
foreach ($line in $dirXml) { [void]$sb.AppendLine($line) }
[void]$sb.AppendLine('    </DirectoryRef>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('  <Fragment>')
[void]$sb.AppendLine('    <ComponentGroup Id="AynFiles">')
foreach ($id in $componentIds) { [void]$sb.AppendLine('      <ComponentRef Id="' + $id + '" />') }
[void]$sb.AppendLine('    </ComponentGroup>')
[void]$sb.AppendLine('  </Fragment>')
[void]$sb.AppendLine('</Wix>')
[IO.File]::WriteAllText($out, $sb.ToString(), [Text.Encoding]::UTF8)
"generated $($componentIds.Count) components"
