@echo off
setlocal
chcp 65001 >nul 2>&1

REM Elimina el acceso directo de la carpeta "Inicio" del usuario actual para que
REM MonitorService.exe deje de lanzarse automaticamente.
REM No detiene el monitor si ya esta ejecutandose: para eso cierra su ventana.

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$startup = [Environment]::GetFolderPath('Startup'); $lnk = Join-Path $startup 'MonitorService.lnk'; if (Test-Path $lnk) { Remove-Item $lnk -Force; Write-Host ''; Write-Host '[OK] Autoarranque desactivado.' -ForegroundColor Green; Write-Host '     MonitorService ya no se lanzara al iniciar sesion.'; Write-Host ''; Write-Host 'Si el monitor esta ejecutandose ahora, cierralo manualmente con la X de su ventana.' } else { Write-Host ''; Write-Host '[INFO] No habia autoarranque configurado.' -ForegroundColor Yellow; Write-Host '       (no he encontrado ningun acceso directo que eliminar)' }"

echo.
echo Pulsa una tecla para cerrar...
pause >nul
