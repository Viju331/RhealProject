# Rheal AI - Start Both API and Web

Write-Host "Starting Rheal AI Project..." -ForegroundColor Green

# Start the API in a new window
Write-Host "`nStarting .NET API on https://localhost:7228..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd D:\RhealProject\RhealAI.API; Write-Host 'Rheal AI API Server' -ForegroundColor Green; dotnet run"

# Wait a moment for API to start
Start-Sleep -Seconds 3

# Start the Angular app in a new window
Write-Host "`nStarting Angular app on http://localhost:4200..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd D:\RhealProject\RhealAI.Web; Write-Host 'Rheal AI Web App' -ForegroundColor Green; npm start"

Write-Host "`n✅ Both servers are starting!" -ForegroundColor Green
Write-Host "API: https://localhost:7228" -ForegroundColor Yellow
Write-Host "Web: http://localhost:4200" -ForegroundColor Yellow
Write-Host "`nPress any key to exit this window..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
