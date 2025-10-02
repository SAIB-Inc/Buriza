# Buriza - Decentralized Cardano Wallet Suite

## Project Overview
Buriza (Japanese for "Blizzard") is a comprehensive, open-source, fully decentralized Cardano wallet software suite built with .NET 9 and Blazor. This Project Catalyst Fund 13 initiative empowers users with maximum control over their wallet infrastructure through self-hosting, custom backend servers, and full transparency.

### Components
- **Buriza.Extension** - Self-verifiable browser extension for seamless dApp interaction
- **Buriza.App** - Cross-platform MAUI app for iOS, Android, macOS, and Windows
- **Buriza.Web** - Progressive Web Application with full wallet functionality
- **Buriza.UI** - Shared component library ensuring consistent UX across platforms

### Key Principles
- **Decentralized** - No reliance on third-party centralized services
- **Self-Verifiable** - Manual installation from source code available
- **UTxO-Native** - Built specifically for Cardano's UTxO model
- **CIP Compliant** - Following Cardano Improvement Proposals for interoperability
- **Open Source** - Apache License 2.0 for complete transparency

## Project Structure
```
Buriza/
├── Buriza.sln                    # Main solution file
└── src/
    ├── Buriza.Extension/          # Browser extension (Blazor WebAssembly)
    ├── Buriza.App/               # MAUI cross-platform app
    ├── Buriza.Web/               # Blazor WebAssembly web app
    └── Buriza.UI/                # Shared UI components library
```

## Technology Stack
- **.NET 9** - Latest .NET framework for high performance
- **Blazor WebAssembly** - Modern web UI framework for browser and extension
- **.NET MAUI** - Cross-platform native development
- **Blazor.BrowserExtension** - Browser extension framework
- **UTxO RPC Standard** - Standardized protocol for UTxO blockchain interaction
- **Cardano APIs** - Direct integration with Cardano node and indexing services

## Development Commands

### Solution Management
```bash
# Create new solution
dotnet new sln --name Buriza

# Add projects to solution
dotnet sln add src/Buriza.UI/Buriza.UI.csproj
dotnet sln add src/Buriza.Extension/Buriza.Extension.csproj
dotnet sln add src/Buriza.Web/Buriza.Web.csproj
dotnet sln add src/Buriza.App/Buriza.App.csproj

# List projects in solution
dotnet sln list
```

### Build Commands
```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/Buriza.Extension/
dotnet build src/Buriza.App/
dotnet build src/Buriza.Web/
dotnet build src/Buriza.UI/
```

### Run Commands
```bash
# Run browser extension (development)
cd src/Buriza.Extension
dotnet run

# Run web application
cd src/Buriza.Web
dotnet run

# Run MAUI app (requires platform-specific setup)
cd src/Buriza.App
dotnet build -t:Run -f net9.0-android    # Android (requires emulator/device)
dotnet build -f net9.0-ios               # iOS
dotnet build -f net9.0-maccatalyst       # macOS

# Android-specific commands
~/Library/Android/sdk/emulator/emulator -list-avds  # List emulators
~/Library/Android/sdk/emulator/emulator -avd <name> &  # Start emulator
~/Library/Android/sdk/platform-tools/adb devices    # Check connected devices
~/Library/Android/sdk/platform-tools/adb uninstall com.saibinc.buriza  # Uninstall app
```

### Development Setup
1. Install .NET 9 SDK
2. For MAUI development, install platform-specific workloads:
   ```bash
   dotnet workload install maui
   dotnet workload install android
   dotnet workload install ios
   dotnet workload install maccatalyst
   ```
3. For Android development:
   - Install Android Studio or Android SDK
   - Install OpenJDK 11+ and add to system PATH
   - Download: https://learn.microsoft.com/java/openjdk/download

### Test Commands
```bash
# Run tests (when available)
dotnet test
```

### Package Commands
```bash
# Publish browser extension
cd src/Buriza.Extension
dotnet publish -c Release

# Publish web app
cd src/Buriza.Web
dotnet publish -c Release

# Package MAUI app
cd src/Buriza.App
dotnet publish -f net9.0-android -c Release
```

## Development Status
🚧 **In Development** - This project is funded by Project Catalyst Fund 13 and currently under active development.

## Cardano Wallet Features
- **UTxO Management** - Native support for Cardano's UTxO model
- **Asset Management** - ADA, native tokens, and NFT support with media rendering
- **dApp Integration** - Seamless connection to Cardano decentralized applications
- **Transaction History** - Comprehensive transaction tracking and organization
- **Address Management** - HD wallet with multiple address derivation
- **Staking** - Built-in delegation and staking pool interaction
- **Hardware Wallet Support** - Integration with Ledger and Trezor devices
- **Multi-Account** - Support for multiple wallet accounts and profiles

## Self-Hosting & Decentralization
- **Custom Backend** - Configure your own Cardano node endpoints
- **Local Installation** - Install from source code for maximum security
- **No Data Collection** - Zero telemetry or user tracking
- **Offline Capability** - Local transaction signing and key management
- **Server Independence** - Reduced reliance on third-party infrastructure

## Project Catalyst Integration
This project is part of SAIB Inc's Project Catalyst Fund 13 proposal: "SAIB: Buriza.Browser - A Dedicated Built-in Browser for the Buriza Wallet". The development follows the funded milestones for creating a decentralized, self-verifiable Cardano wallet ecosystem.