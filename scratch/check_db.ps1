Add-Type -Path "D:\GHG\AUTO\src\Mal.UniversalScada.UI.Wpf\bin\Debug\net8.0-windows\Microsoft.Data.Sqlite.dll"
$c = New-Object Microsoft.Data.Sqlite.SqliteConnection("Data Source=d:\GHG\AUTO\scada_config.db")
$c.Open()
$cmd = $c.CreateCommand()
$cmd.CommandText = "SELECT ViewId, Name, IsDefault FROM UiViews"
$r = $cmd.ExecuteReader()
while($r.Read()){ 
    Write-Output ("View: " + $r.GetString(0) + " | " + $r.GetString(1) + " | Default=" + $r.GetInt32(2)) 
}
$r.Close()

$cmd.CommandText = "SELECT Username, Role, IsEnabled FROM Users"
$r = $cmd.ExecuteReader()
while($r.Read()){ 
    Write-Output ("User: " + $r.GetString(0) + " | Role=" + $r.GetInt32(1) + " | Enabled=" + $r.GetInt32(2)) 
}
$r.Close()

$cmd.CommandText = "SELECT Username, ViewId FROM UserViewPermissions"
$r = $cmd.ExecuteReader()
while($r.Read()){ 
    Write-Output ("Perm: " + $r.GetString(0) + " -> " + $r.GetString(1)) 
}
$r.Close()
$c.Close()
