# Vanq CLI

Command-line interface tool for managing the Vanq.API backend.

## Features

- 🔐 **Secure Authentication** - Cross-platform encrypted credential storage (DPAPI on Windows, AES-256 on Linux/macOS)
- 🔄 **Automatic Token Refresh** - JWT tokens refreshed automatically when expired
- 🌍 **Multi-Environment Support** - Manage multiple profiles (dev, staging, production)
- 📊 **Multiple Output Formats** - JSON, Table, and CSV output
- 📈 **Anonymous Telemetry** - Optional opt-out anonymous usage metrics
- 🎨 **Rich Terminal UI** - Colored tables and formatted output via Spectre.Console

## Installation

### As .NET Global Tool

```bash
dotnet tool install -g Vanq.CLI
```

### From Source

```bash
cd tools/Vanq.CLI
dotnet build
dotnet run -- --help
```

### Publish Standalone

```bash
# Windows
dotnet publish -c Release -r win-x64 --self-contained

# Linux
dotnet publish -c Release -r linux-x64 --self-contained

# macOS
dotnet publish -c Release -r osx-x64 --self-contained
```

## Quick Start

```bash
# View help
vanq --help

# Configure API endpoint
vanq config add-profile prod https://api.vanq.io

# Login
vanq login
# Email: admin@vanq.io
# Password: ********

# Check authentication
vanq whoami

# List roles
vanq role list

# Check API health
vanq health
```

## Global Options

Available on all commands:

- `-v, --verbose` - Enable verbose output with detailed logging
- `-o, --output <json|table|csv>` - Output format (default: table)
- `-p, --profile <name>` - Override active profile
- `--no-color` - Disable colored output
- `-f, --force` - Bypass confirmation prompts
- `--version` - Show version information
- `-h, --help` - Show help

## Commands

### Authentication

```bash
# Login with email/password
vanq login

# Logout (revokes refresh token)
vanq logout

# Show current user info
vanq whoami
```

### Configuration Management

```bash
# List all profiles
vanq config list

# Add new profile
vanq config add-profile staging https://staging-api.vanq.io

# Switch profile
vanq config set-profile staging

# Enable/disable telemetry
vanq config telemetry enable
vanq config telemetry disable
```

### Role Management

```bash
# List all roles
vanq role list

# List roles as JSON
vanq role list --output json

# Create new role
vanq role create moderator "Content Moderator" "Manages user content"
```

### Feature Flags

```bash
# List flags for current environment
vanq feature-flag list

# List flags for all environments
vanq feature-flag list --all

# Enable a feature flag
vanq feature-flag set new-feature true --reason "Gradual rollout"

# Disable a feature flag
vanq feature-flag set new-feature false --reason "Rollback due to bug"
```

### System Parameters

```bash
# List all system parameters
vanq system-param list

# List parameters by category
vanq system-param list --category auth

# Get specific parameter
vanq system-param get auth.password.minLength
```

### Health Check

```bash
# Check API health
vanq health

# Verbose health check
vanq health --verbose
```

## Configuration Files

CLI stores configuration and credentials in `~/.vanq/`:

```
~/.vanq/
├── config.json          # Profiles and settings (plain text)
├── credentials.bin      # Encrypted credentials
└── logs/
    └── vanq-cli.log     # Command logs
```

### config.json Structure

```json
{
  "CurrentProfile": "default",
  "Profiles": [
    {
      "Name": "default",
      "ApiEndpoint": "http://localhost:5000",
      "OutputFormat": "table"
    },
    {
      "Name": "prod",
      "ApiEndpoint": "https://api.vanq.io",
      "OutputFormat": "json"
    }
  ],
  "Telemetry": {
    "Enabled": true,
    "ConsentGiven": true,
    "ConsentDate": "2025-10-03T14:30:00Z",
    "AnonymousId": "550e8400-e29b-41d4-a716-446655440000",
    "Endpoint": "http://localhost:5000/api/telemetry/cli"
  }
}
```

## Security

### Credential Storage

- **Windows**: Uses DPAPI (Data Protection API) for encryption
- **Linux/macOS**: Uses AES-256 encryption with machine-specific key derived from `MachineName` + `UserName`

Credentials are stored in `~/.vanq/credentials.bin` and include:
- Access token (JWT)
- Refresh token
- Expiration timestamp
- Email

### Token Refresh

The CLI automatically refreshes access tokens when they expire or are about to expire (within 2 minutes of expiration). If refresh fails, you'll be prompted to login again.

## Telemetry & Privacy

On first run, you'll be asked to opt-in to anonymous telemetry.

**What we collect:**
- Command names (e.g., `role list`, `login`)
- Success/failure status
- Execution time
- CLI version, OS platform

**What we DON'T collect:**
- Your email, passwords, or tokens
- API URLs or endpoints
- Parameter values or data
- Any personally identifiable information

**Opt-out anytime:**
```bash
vanq config telemetry disable
```

## Examples

### Managing Roles

```bash
# List all roles as table
vanq role list

# Create role with permissions
vanq role create editor "Content Editor" "Can edit posts"

# View as JSON
vanq role list --output json
```

### Feature Flag Workflow

```bash
# List current environment flags
vanq feature-flag list

# Enable beta feature
vanq feature-flag set beta-ui true --reason "Enable for 10% users"

# Check with different profile
vanq feature-flag list --profile staging
```

### Multi-Environment Setup

```bash
# Add profiles
vanq config add-profile dev http://localhost:5000
vanq config add-profile staging https://staging-api.vanq.io
vanq config add-profile prod https://api.vanq.io

# Login to each environment
vanq config set-profile dev
vanq login

vanq config set-profile staging
vanq login

vanq config set-profile prod
vanq login

# Use with different profiles
vanq role list --profile dev
vanq role list --profile staging
vanq role list --profile prod
```

## Error Codes

The CLI uses standard exit codes:

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Generic error |
| 2 | Authentication failed |
| 3 | Permission denied |
| 4 | Resource not found |
| 5 | Validation failed |

Use in scripts:

```bash
#!/bin/bash
vanq health
if [ $? -eq 0 ]; then
  echo "API is healthy"
else
  echo "API is down"
  exit 1
fi
```

## Requirements

- .NET 10.0 RC or later
- PostgreSQL-backed Vanq.API instance
- Network connectivity to API endpoint

## Troubleshooting

### "Failed to connect to API"

Check that:
1. API is running: `vanq health`
2. Correct profile is active: `vanq config list`
3. Network connectivity: `ping api.vanq.io`

### "Authentication failed"

Try logging in again:
```bash
vanq logout
vanq login
```

### "Credentials corrupted"

Delete credentials and login again:
```bash
# Windows
del %USERPROFILE%\.vanq\credentials.bin

# Linux/macOS
rm ~/.vanq/credentials.bin

vanq login
```

## Development

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run -- <command>
```

### Publish as Tool

```bash
dotnet pack
dotnet tool install -g --add-source ./nupkg Vanq.CLI
```

## License

MIT

## Support

- Issues: https://github.com/vanq/vanq-backend/issues
- Documentation: https://docs.vanq.io/cli

---

**Version:** 0.1.0
**Status:** Production-ready (SPEC-0012 v0.1.0)
