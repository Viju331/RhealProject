# Rheal AI - Quick Start Guide

## Prerequisites
- .NET 10 SDK installed
- Node.js and npm installed
- GitHub Personal Access Token (for AI models)

## Setup Steps

### 1. Configure GitHub Token
Edit `RhealAI.API/appsettings.Development.json` and add your GitHub token:

```json
{
  "GitHub": {
    "Token": "YOUR_GITHUB_TOKEN_HERE",
    "Model": "gpt-4o"
  }
}
```

To get a GitHub token:
1. Go to https://github.com/settings/tokens
2. Generate a new token (classic)
3. Select scopes: `read:packages` and `read:user`
4. Copy the token and paste it in the config file

### 2. Start the Application

#### Option A: Use the Start Script (Recommended)
```powershell
cd D:\RhealProject
.\start.ps1
```

#### Option B: Start Manually

**Terminal 1 - API:**
```powershell
cd D:\RhealProject\RhealAI.API
dotnet run
```

**Terminal 2 - Web:**
```powershell
cd D:\RhealProject\RhealAI.Web
npm start
```

### 3. Access the Application
- **Web UI**: http://localhost:4200
- **API**: https://localhost:7228
- **API Docs**: https://localhost:7228/openapi/v1.json

## API Endpoints

### Repository
- `POST /api/repository/upload` - Upload ZIP file
- `GET /api/repository/{id}` - Get repository details

### Analysis
- `POST /api/analysis/{repositoryId}/analyze` - Start analysis
- `GET /api/analysis/report/{reportId}` - Get report
- `GET /api/analysis/report/{reportId}/export/json` - Export JSON
- `GET /api/analysis/report/{reportId}/export/pdf` - Export PDF

### Standards
- `GET /api/standards/repository/{repositoryId}` - Get standards

## CORS Configuration
✅ CORS is configured to allow:
- http://localhost:4200
- https://localhost:4200
- All HTTP methods
- All headers
- Credentials

## Troubleshooting

### CORS Errors
If you still see CORS errors:
1. Make sure the API is running on https://localhost:7228
2. Check browser console for the exact error
3. Verify the Angular app is running on http://localhost:4200

### GitHub Token Issues
If you get authentication errors:
1. Verify your token is valid
2. Check token has correct permissions
3. Try regenerating the token

### Port Conflicts
If ports are in use:
- API: Change in `RhealAI.API/Properties/launchSettings.json`
- Web: Change in `RhealAI.Web/angular.json` (port option)

## Testing the Connection

1. Open http://localhost:4200
2. Go to Upload page
3. Upload a ZIP file
4. Check browser Network tab - you should see successful requests to https://localhost:7228

## Architecture
```
┌─────────────────┐         ┌──────────────────┐
│  Angular Web    │────────▶│   .NET 10 API    │
│  (Port 4200)    │  HTTPS  │  (Port 7228)     │
└─────────────────┘         └──────────────────┘
                                     │
                                     ▼
                            ┌──────────────────┐
                            │  GitHub Models   │
                            │    (OpenAI)      │
                            └──────────────────┘
```
