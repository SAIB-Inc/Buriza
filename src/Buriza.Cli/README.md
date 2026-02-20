# Buriza.Cli

Cross‑platform terminal wallet built on Buriza.Core + Buriza.Data.

## Features

- Create/import wallets
- List/set active wallet
- Show receive address
- Balance, assets, UTXOs, transaction history
- Send transactions (build/sign/submit)
- Set network (mainnet/preprod/preview)
- Set/clear custom provider endpoints + API keys
- Export mnemonic (password required)

## Storage

CLI runs in **VaultEncryption** mode with **in‑memory storage only**.
Data is not persisted between runs.

## Config

Edit `appsettings.json` to set provider endpoints and API keys.

## Run

```bash
dotnet build src/Buriza.Cli/Buriza.Cli.csproj
dotnet run --project src/Buriza.Cli/Buriza.Cli.csproj
```
