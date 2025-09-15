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

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- For MAUI development:
  ```bash
  dotnet workload install maui
  dotnet workload install android
  dotnet workload install ios
  dotnet workload install maccatalyst
  ```

### Build & Run

```bash
# Clone the repository
git clone https://github.com/saib-inc/Buriza.git
cd Buriza

# Install CSS dependencies
cd src/Buriza.UI && bun install

# Build the entire solution
dotnet build

# Run CSS watch (in separate terminal)
cd src/Buriza.UI && bun watch

# Run specific projects
cd src/Buriza.Web && dotnet run          # Web app
cd src/Buriza.Extension && dotnet build -c Release  # Browser extension

# Load in Chrome/Edge:
# 1. Open chrome://extensions/
# 2. Enable "Developer mode" 
# 3. Click "Load unpacked"
# 4. Select: src/Buriza.Extension/bin/Release/net9.0/browserextension/

### Desktop Application

```bash
# Windows
cd src/Buriza.App && dotnet build -f net9.0-windows && dotnet run -f net9.0-windows

# macOS
cd src/Buriza.App && dotnet build -f net9.0-maccatalyst && dotnet run -f net9.0-maccatalyst

### Phone Simulator

```bash
# Install required workloads
dotnet workload restore

# iOS Simulator (requires macOS + Xcode)
cd src/Buriza.App && dotnet build -t:Run -f net9.0-ios

# Android Emulator (requires Android SDK)
cd src/Buriza.App && dotnet build -t:Run -f net9.0-android
```

### Physical Device

```bash
# iOS Device (requires developer provisioning)
cd src/Buriza.App && dotnet build -t:Run -f net9.0-ios -p:RuntimeIdentifier=ios-arm64

# Android Device (requires USB debugging enabled)
cd src/Buriza.App && dotnet build -t:Run -f net9.0-android -p:RuntimeIdentifier=android-arm64
```

## 📱 Platform Support

### Browser Extension
- **Chrome/Chromium** - Full support with self-verifiable installation
- **Firefox** - Native WebExtension support
- **Safari** - macOS and iOS compatibility
- **Edge** - Chromium-based extension support

### Mobile & Desktop
- **iOS** - 15.0+ (Native MAUI app)
- **Android** - API 21+ (Native MAUI app)
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