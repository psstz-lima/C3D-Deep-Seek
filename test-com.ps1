Add-Type -AssemblyName System.Runtime.InteropServices

$progIDs = @(
    "AutoCAD.Application.25.1",
    "AutoCAD.Application.25", 
    "AutoCAD.Application.24",
    "AutoCAD.Application",
    "AeccXUiLand.AeccApplication.14.0"
)

foreach ($id in $progIDs) {
    try {
        $app = [System.Runtime.InteropServices.Marshal]::GetActiveObject($id)
        Write-Output "OK: $id | $($app.Name) | v$($app.Version)"
    } catch {
        Write-Output "NO: $id"
    }
}
