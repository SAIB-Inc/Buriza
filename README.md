# Buriza - Decentralized Cardano Wallet Suite

> 🚧 **In Development** - This project is currently under active development as part of Project Catalyst Fund 13.

Buriza (Japanese for "Blizzard") is a comprehensive, open-source, fully decentralized Cardano wallet software suite spanning mobile, browser, and desktop applications. Built with .NET 9 and Blazor, Buriza empowers users with maximum control over their entire wallet infrastructure through self-hosting, custom backend servers, and full transparency.

## 🚀 Key Features

### 🔒 **Decentralized & Self-Verifiable**
- Complete control over wallet infrastructure
- Self-hostable backend servers
- Manual installation from source code
- No reliance on third-party centralized services

### 🌐 **Multi-Platform Support**
- **Buriza.Extension** - Self-verifiable browser extension for seamless dApp interaction
- **Buriza.App** - Cross-platform mobile/desktop app (iOS, Android, macOS, Windows)
- **Buriza.Web** - Progressive Web Application
- **Buriza.UI** - Shared component library for consistent UX

### ₳ **Cardano-Native Features**
- Full UTxO-based blockchain interaction through UTxO RPC standard
- CIP compliance for maximum interoperability
- Advanced transaction management and asset rendering
- Comprehensive NFT, token, and media support

### 🛡️ **Enhanced Security & Privacy**
- Configurable backend endpoints
- Local transaction signing
- No data collection or tracking
- Open-source transparency (Apache License)

## 🏗️ Architecture

```
Buriza/
├── src/
│   ├── Buriza.Extension/     # Browser extension (Blazor WebAssembly)
│   ├── Buriza.App/          # MAUI cross-platform app
│   ├── Buriza.Web/          # Blazor WebAssembly web app
│   └── Buriza.UI/           # Shared UI components library
└── Buriza.sln              # Solution file
```

## 🛠️ Technology Stack

- **.NET 9** - Latest .NET framework for high performance
- **Blazor WebAssembly** - Modern web UI framework for browser and extension
- **.NET MAUI** - Cross-platform native development
- **Blazor.BrowserExtension** - Browser extension framework
- **UTxO RPC Standard** - Standardized protocol for UTxO blockchain interaction
- **Apache License** - Open-source licensing for transparency

## 🚀 Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (v9.0.0+)
- [Bun](https://bun.sh/) (v1.2.0+)
- [Git](https://git-scm.com/) (v2.39.0+)
- For MAUI development:
  ```bash
  dotnet workload restore
  ```

### Build & Run

```bash
# Clone the repository
git clone https://github.com/saib-inc/Buriza.git
cd Buriza

# Restore workloads for MAUI development
dotnet workload restore

# Install CSS dependencies
cd src/Buriza.UI/wwwroot && bun install

# Build the entire solution
dotnet build

# Run CSS watch (in separate terminal)
cd src/Buriza.UI/wwwroot && bun watch

# Run specific projects
cd src/Buriza.Web && dotnet run          # Web app
cd src/Buriza.Extension && dotnet build -c Release  # Browser extension

# Load in browser:
# 1. Navigate to browser's extension management page
# 2. Enable "Developer mode"
# 3. Click "Load unpacked"
# 4. Select: src/Buriza.Extension/bin/Release/net9.0/browserextension/

### Desktop Application

```bash
# Windows
cd src/Buriza.App && dotnet build -f net9.0-windows && dotnet run -f net9.0-windows

# macOS
cd src/Buriza.App && dotnet build -f net9.0-maccatalyst && dotnet run -f net9.0-maccatalyst

### iOS Simulator

```bash
# Install required workloads
dotnet workload restore

# Build for iOS simulator
cd src/Buriza.App && dotnet build . -f net9.0-ios

# Install and launch on simulator
xcrun simctl boot "iPhone 16 Pro"  # or any available device
xcrun simctl install booted bin/Debug/net9.0-ios/iossimulator-arm64/Buriza.App.app
xcrun simctl launch booted com.saibinc.buriza
```

### Physical iPhone

```bash
# Prerequisites:
# 1. Change bundle ID to com.yourname.buriza in Buriza.App.csproj
# 2. Create Xcode project with same bundle ID to generate provisioning profile

# Build for physical device
cd src/Buriza.App && dotnet build . -f net9.0-ios -p:RuntimeIdentifier=ios-arm64

# Get device ID
xcrun devicectl list devices

# Install to connected device
xcrun devicectl device install app --device [device-id] bin/Debug/net9.0-ios/ios-arm64/Buriza.App.app
```

### Android Emulator & Device

```bash
# Prerequisites:
# 1. Install Android Studio or Android SDK
# 2. Install OpenJDK 11+ and add to system PATH
#    - Download: https://learn.microsoft.com/java/openjdk/download

# Start Android emulator
~/Library/Android/sdk/emulator/emulator -avd <emulator-name> &

# Wait for device to boot
~/Library/Android/sdk/platform-tools/adb wait-for-device

# Build and deploy to emulator/device
cd src/Buriza.App && dotnet build -t:Run -f net9.0-android

# Or build for specific device architecture
dotnet build -t:Run -f net9.0-android -p:RuntimeIdentifier=android-arm64

# Useful commands:
# List available emulators
~/Library/Android/sdk/emulator/emulator -list-avds

# Check connected devices
~/Library/Android/sdk/platform-tools/adb devices

# Uninstall app
~/Library/Android/sdk/platform-tools/adb uninstall com.saibinc.buriza

# View app logs
~/Library/Android/sdk/platform-tools/adb logcat | grep -i buriza
```

## 📱 Platform Support

### Browser Extension
- **Chrome/Chromium** - Full support with self-verifiable installation
- **Firefox** - Native WebExtension support
- **Safari** - macOS and iOS compatibility
- **Edge** - Chromium-based extension support

### Mobile & Desktop
- **iOS** - 15.0+ (Native MAUI app)
- **Android** - 6.0+ / API 23+ (Native MAUI app)
- **Windows** - 10+ (Native MAUI app)
- **macOS** - 12+ (Native MAUI app)

## 🌟 Project Catalyst

This project is funded by [Project Catalyst Fund 13](https://projectcatalyst.io/) as part of SAIB Inc's commitment to advancing the Cardano ecosystem through innovative, open-source wallet solutions.

**Proposal**: *SAIB: Buriza.Browser - A Dedicated Built-in Browser for the Buriza Wallet*

## 🔧 Development

See [CLAUDE.md](CLAUDE.md) for detailed development commands and project context.

## 📄 License

This project is open-source software licensed under the **Apache License 2.0**.

## 🤝 Contributing

We welcome contributions from the Cardano community! This project is developed by **SAIB Inc** with transparency and community collaboration in mind.

For contribution guidelines and development setup, please see our documentation.