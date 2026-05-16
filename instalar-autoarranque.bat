@echo off
setlocal
chcp 65001 >nul 2>&1

REM Crea un acceso directo en la carpeta "Inicio" del usuario actual para que
REM MonitorService.exe se lance automaticamente al iniciar sesion en Windows,
REM en una ventana minimizada. No requiere permisos de administrador.

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$dir = '%~dp0'.TrimEnd('\'); $exe = Join-Path $dir 'MonitorService.exe'; if (-not (Test-Path $exe)) { Write-Host '[ERROR] No encuentro MonitorService.exe en esta carpeta.' -ForegroundColor Red; Write-Host '         Pon este .bat junto al MonitorService.exe e intenta de nuevo.'; exit 1 }; $startup = [Environment]::GetFolderPath('Startup'); $lnk = Join-Path $startup 'MonitorService.lnk'; $sh = New-Object -ComObject WScript.Shell; $sc = $sh.CreateShortcut($lnk); $sc.TargetPath = $exe; $sc.WorkingDirectory = $dir; $sc.WindowStyle = 7; $sc.Description = 'MonitorService - vigilancia de RSS y webs'; $sc.Save(); Write-Host ''; Write-Host '[OK] MonitorService se iniciara automaticamente al encender el PC.' -ForegroundColor Green; Write-Host '     Ventana minimizada en la barra de tareas.'; Write-Host ''; Write-Host 'Acceso directo creado en:'; Write-Host ('  ' + $lnk); Write-Host ''; Write-Host 'Para desinstalarlo: doble-clic en desinstalar-autoarranque.bat.'"

echo.
echo Pulsa una tecla para cerrar...
pause >nul
