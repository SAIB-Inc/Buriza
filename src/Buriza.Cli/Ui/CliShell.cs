using System.Collections;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buriza.Cli.Services;
using Buriza.Core.Chain.Cardano;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Chain;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Services;
using Buriza.Data.Models;
using Buriza.Data.Providers;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Spectre.Console;

namespace Buriza.Cli.Ui;

public sealed class CliShell(WalletManagerService walletManager, ChainProviderSettings settings, IBurizaChainProviderFactory providerFactory, CliAppStateService appState)
{
    private readonly WalletManagerService _walletManager = walletManager;
    private readonly ChainProviderSettings _settings = settings;
    private readonly IBurizaChainProviderFactory _providerFactory = providerFactory;
    private readonly CliAppStateService _appState = appState;
    private BurizaWallet? _activeWallet;
    private HeartbeatService? _heartbeat;
    private IBurizaChainProvider? _heartbeatProvider;
    private string? _heartbeatKey;
    private ulong _lastTipSlot;
    private ulong _lastTipHeight;
    private string _lastTipHash = string.Empty;
    private DateTime? _lastTipAtUtc;
    private ulong? _lastBalanceLovelace;
    private bool _balanceRefreshing;
    private bool _needsRender;

    public async Task RunAsync()
    {
        while (true)
        {
            _activeWallet ??= await _walletManager.GetActiveAsync();

            if (_activeWallet is null)
            {
                string startChoice = await RunMenuLoopAsync(
                    "Get started",
                    ["Create wallet", "Import wallet", "Exit"]);

                switch (startChoice)
                {
                    case "Create wallet":
                        await ExecuteWithHandlingAsync(CreateWalletAsync);
                        break;
                    case "Import wallet":
                        await ExecuteWithHandlingAsync(ImportWalletAsync);
                        break;
                    case "Exit":
                        return;
                }

                continue;
            }

            string choice = await RunMenuLoopAsync(
                "Select a section",
                ["Wallets", "Account", "Transactions", "Network", "Protocol", "Exit"]);

            switch (choice)
            {
                case "Wallets":
                    await ShowWalletsMenuAsync();
                    break;
                case "Account":
                    await ShowAccountMenuAsync();
                    break;
                case "Transactions":
                    await ShowTransactionsMenuAsync();
                    break;
                case "Network":
                    await ShowNetworkMenuAsync();
                    break;
                case "Protocol":
                    await ShowProtocolMenuAsync();
                    break;
                case "Exit":
                    return;
            }
        }
    }

    private async Task ShowWalletsMenuAsync()
    {
        while (true)
        {
            bool isUnlocked = _activeWallet?.IsUnlocked == true;
            string lockAction = isUnlocked ? "Lock wallet" : "Unlock wallet";

            string choice = await RunMenuLoopAsync(
                "Wallets",
                [lockAction, "List wallets", "Set active wallet", "Export mnemonic", "Create wallet", "Import wallet", "Back"]);

            switch (choice)
            {
                case "Unlock wallet":
                    await ExecuteWithHandlingAsync(UnlockWalletAsync);
                    break;
                case "Lock wallet":
                    LockWallet();
                    break;
                case "List wallets":
                    await ExecuteWithHandlingAsync(ListWalletsAsync);
                    break;
                case "Set active wallet":
                    await ExecuteWithHandlingAsync(SetActiveWalletAsync);
                    break;
                case "Export mnemonic":
                    await ExecuteWithHandlingAsync(ExportMnemonicAsync);
                    break;
                case "Create wallet":
                    await ExecuteWithHandlingAsync(() => CreateWalletAsync());
                    break;
                case "Import wallet":
                    await ExecuteWithHandlingAsync(() => ImportWalletAsync());
                    break;
                case "Back":
                    return;
            }
        }
    }

    private async Task ShowAccountMenuAsync()
    {
        while (true)
        {
            string choice = await RunMenuLoopAsync(
                "Account",
                ["Show receive address", "Show balance", "Show assets", "Show UTXOs", "Back"]);

            switch (choice)
            {
                case "Show receive address":
                    await ExecuteWithHandlingAsync(ShowReceiveAddressAsync);
                    break;
                case "Show balance":
                    await ExecuteWithHandlingAsync(ShowBalanceAsync);
                    break;
                case "Show assets":
                    await ExecuteWithHandlingAsync(ShowAssetsAsync);
                    break;
                case "Show UTXOs":
                    await ExecuteWithHandlingAsync(ShowUtxosAsync);
                    break;
                case "Back":
                    return;
            }
        }
    }

    private async Task ShowTransactionsMenuAsync()
    {
        while (true)
        {
            string choice = await RunMenuLoopAsync(
                "Transactions",
                ["Send transaction", "Back"]);

            switch (choice)
            {
                case "Send transaction":
                    await ExecuteWithHandlingAsync(SendTransactionAsync);
                    break;
                case "Back":
                    return;
            }
        }
    }

    private async Task ShowNetworkMenuAsync()
    {
        while (true)
        {
            string choice = await RunMenuLoopAsync(
                "Network",
                ["Set network", "Set custom provider", "Clear custom provider", "Back"]);

            switch (choice)
            {
                case "Set network":
                    await ExecuteWithHandlingAsync(SetNetworkAsync);
                    break;
                case "Set custom provider":
                    await ExecuteWithHandlingAsync(SetCustomProviderAsync);
                    break;
                case "Clear custom provider":
                    await ExecuteWithHandlingAsync(ClearCustomProviderAsync);
                    break;
                case "Back":
                    return;
            }
        }
    }

    private async Task ShowProtocolMenuAsync()
    {
        while (true)
        {
            string choice = await RunMenuLoopAsync(
                "Protocol",
                ["Show protocol parameters", "Back"]);

            switch (choice)
            {
                case "Show protocol parameters":
                    await ExecuteWithHandlingAsync(ShowProtocolParametersAsync);
                    break;
                case "Back":
                    return;
            }
        }
    }

    private async Task ShowProtocolParametersAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        ChainInfo chainInfo = ChainRegistry.Get(active.ActiveChain, active.Network);
        using IBurizaChainProvider provider = _providerFactory.CreateProvider(chainInfo);
        BurizaUtxoRpcProvider cardanoProvider = provider as BurizaUtxoRpcProvider
            ?? throw new InvalidOperationException("Protocol parameters are only available for Cardano providers.");
        ProtocolParams parameters = await cardanoProvider.GetParametersAsync();

        Table table = new();
        table.AddColumn("Field");
        table.AddColumn("Value");

        IOrderedEnumerable<PropertyInfo> props = parameters.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(p => p.Name);

        foreach (PropertyInfo? prop in props)
        {
            object? value = prop.GetValue(parameters);
            if (value is null)
                continue;
            table.AddRow(prop.Name, Markup.Escape(FormatProtocolValue(value)));
        }

        AnsiConsole.Write(table);
        Pause();
    }

    // Hide framework/CBOR metadata fields and render nested objects as structured JSON.
    private static readonly HashSet<string> HiddenProtocolFields =
    [
        "Raw",
        "CborTypeName",
        "ConstrIndex",
        "IsIndefinite"
    ];
    private const int CollectionPreviewLimit = 12;

    private static string FormatProtocolValue(object value)
    {
        object? normalized = NormalizeProtocolValue(value, depth: 0);
        if (normalized is null)
            return string.Empty;

        if (normalized is string text)
            return text;

        return JsonSerializer.Serialize(normalized);
    }

    private static object? NormalizeProtocolValue(object? value, int depth)
    {
        if (value is null)
            return null;

        if (depth > 8)
            return "...";

        Type type = value.GetType();
        if (type.IsPrimitive || value is decimal || value is string || value is Guid || value is DateTime || value is DateTimeOffset || value is Enum)
            return value;

        if (value is byte[] bytes)
            return $"[omitted {bytes.Length} bytes]";

        if (value is IDictionary dictionary)
            return NormalizeDictionary(dictionary, depth);

        if (value is IEnumerable enumerable and not string)
            return NormalizeEnumerable(enumerable, depth);

        Dictionary<string, object?> map = [];
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
                continue;
            if (HiddenProtocolFields.Contains(property.Name))
                continue;

            object? propertyValue = property.GetValue(value);
            if (propertyValue is null)
                continue;

            map[property.Name] = NormalizeProtocolValue(propertyValue, depth + 1);
        }

        return map.Count == 0 ? value.ToString() : map;
    }

    private static Dictionary<string, object?> NormalizeDictionary(IDictionary dictionary, int depth)
    {
        Dictionary<string, object?> output = [];
        int totalCount = 0;
        foreach (DictionaryEntry entry in dictionary)
        {
            totalCount++;
            if (output.Count >= CollectionPreviewLimit)
                continue;

            string key = entry.Key?.ToString() ?? "<null>";
            output[key] = NormalizeProtocolValue(entry.Value, depth + 1);
        }

        if (totalCount > CollectionPreviewLimit)
        {
            return new Dictionary<string, object?>
            {
                ["Count"] = totalCount,
                ["Preview"] = output,
                ["Truncated"] = true
            };
        }

        return output;
    }

    private static object NormalizeEnumerable(IEnumerable enumerable, int depth)
    {
        List<object?> preview = [];
        int totalCount = 0;

        foreach (object? item in enumerable)
        {
            totalCount++;
            if (preview.Count < CollectionPreviewLimit)
                preview.Add(NormalizeProtocolValue(item, depth + 1));
        }

        if (totalCount > CollectionPreviewLimit)
        {
            bool allNumeric = preview.All(IsNumericLike);
            if (allNumeric)
                return $"[numeric collection omitted, count={totalCount}]";

            return new Dictionary<string, object?>
            {
                ["Count"] = totalCount,
                ["Preview"] = preview,
                ["Truncated"] = true
            };
        }

        return preview;
    }

    private static bool IsNumericLike(object? value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private async Task CreateWalletAsync()
    {
        string? name = PromptOptional("Wallet name");
        if (string.IsNullOrWhiteSpace(name))
            return;

        string? password = PromptAndConfirmPassword("New password");
        if (string.IsNullOrEmpty(password))
            return;

        string mnemonic = "";
        WalletManagerService.GenerateMnemonic(24, span =>
        {
            mnemonic = span.ToString();
        });

        AnsiConsole.Write(new Panel(new Markup($"[bold]Write down your mnemonic:[/]\n\n[white]{mnemonic}[/]"))
            .BorderColor(Color.Grey)
            .RoundedBorder()
            .Padding(1, 0, 1, 0));

        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        BurizaWallet wallet;
        try
        {
            wallet = await _walletManager.CreateAsync(name, mnemonicBytes, passwordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
        await wallet.SetActiveChainAsync(ChainRegistry.CardanoPreview);
        await _walletManager.SetActiveAsync(wallet.Id);
        _activeWallet = wallet;
        await CacheActiveAddressAsync(wallet);
        AnsiConsole.MarkupLine("[bold]Wallet created and set active.[/]");
        Pause();
    }

    private async Task ImportWalletAsync()
    {
        string? name = PromptOptional("Wallet name");
        if (string.IsNullOrWhiteSpace(name))
            return;

        string? mnemonic = PromptOptional("Mnemonic");
        if (string.IsNullOrWhiteSpace(mnemonic))
            return;

        string? password = PromptAndConfirmPassword("New password");
        if (string.IsNullOrEmpty(password))
            return;

        byte[] mnemonicBytes = Encoding.UTF8.GetBytes(mnemonic);
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        BurizaWallet wallet;
        try
        {
            wallet = await _walletManager.CreateAsync(name, mnemonicBytes, passwordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mnemonicBytes);
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
        await wallet.SetActiveChainAsync(ChainRegistry.CardanoPreview);
        await _walletManager.SetActiveAsync(wallet.Id);
        _activeWallet = wallet;
        await CacheActiveAddressAsync(wallet);
        AnsiConsole.MarkupLine("[bold]Wallet imported and set active.[/]");
        Pause();
    }

    private async Task UnlockWalletAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        if (active.IsUnlocked)
        {
            AnsiConsole.MarkupLine("[grey]Wallet is already unlocked.[/]");
            Pause();
            return;
        }

        string? password = PromptOptional("Password", secret: true);
        if (string.IsNullOrWhiteSpace(password))
            return;

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try { await active.UnlockAsync(passwordBytes); }
        finally { CryptographicOperations.ZeroMemory(passwordBytes); }

        await CacheActiveAddressAsync(active);
        AnsiConsole.MarkupLine("[bold]Wallet unlocked.[/]");
        Pause();
    }

    private void LockWallet()
    {
        if (_activeWallet is not null)
            _appState.ClearWalletCache(_activeWallet.Id);
        _activeWallet?.Lock();
        AnsiConsole.MarkupLine("[bold]Wallet locked.[/]");
        Pause();
    }

    private async Task ListWalletsAsync()
    {
        IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();
        if (wallets.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No wallets found.[/]");
            return;
        }

        Table table = new();
        table.AddColumn("Id");
        table.AddColumn("Name");
        table.AddColumn("Network");
        table.AddColumn("Active");

        foreach (BurizaWallet wallet in wallets)
        {
            table.AddRow(
                wallet.Id.ToString("N"),
                wallet.Profile.Name,
                wallet.Network,
                _activeWallet?.Id == wallet.Id ? "Yes" : "No");
        }

        AnsiConsole.Write(table);
        Pause();
    }

    private async Task ShowReceiveAddressAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        ChainAddressData? data = await active.GetAddressInfoAsync();
        if (data is null || string.IsNullOrEmpty(data.Address))
        {
            AnsiConsole.MarkupLine("[yellow]Wallet is locked.[/] Unlock it first to derive the address.");
            Pause();
            return;
        }
        AnsiConsole.MarkupLine($"[bold]Receive address:[/] {data.Address}");
        if (data is CardanoAddressData cardano)
            AnsiConsole.MarkupLine($"[bold]Staking address:[/] {cardano.StakingAddress}");
        Pause();
    }

    private async Task ShowBalanceAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        ulong balance = await active.GetBalanceAsync();
        decimal ada = balance / 1_000_000m;
        AnsiConsole.MarkupLine($"[bold]Balance:[/] {ada:0.000000} ADA");
        Pause();
    }

    private async Task ShowAssetsAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        IReadOnlyList<Asset> assets = await active.GetAssetsAsync();
        if (assets.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No assets found.[/]");
            Pause();
            return;
        }

        Table table = new();
        table.AddColumn("Unit");
        table.AddColumn("Quantity");
        foreach (Asset asset in assets)
            table.AddRow(asset.Unit, asset.Quantity.ToString());
        AnsiConsole.Write(table);
        Pause();
    }

    private async Task ShowUtxosAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        IReadOnlyList<Utxo> utxos = await active.GetUtxosAsync();
        if (utxos.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No UTXOs found.[/]");
            Pause();
            return;
        }

        Table table = new();
        table.AddColumn("TxId");
        table.AddColumn("Index");
        table.AddColumn("Value");
        foreach (Utxo utxo in utxos)
            table.AddRow(utxo.TxHash, utxo.OutputIndex.ToString(), utxo.Value.ToString());
        AnsiConsole.Write(table);
        Pause();
    }

    private async Task SendTransactionAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();

        if (!active.IsUnlocked)
        {
            string? password = PromptOptional("Password", secret: true);
            if (string.IsNullOrWhiteSpace(password))
                return;

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            try { await active.UnlockAsync(passwordBytes); }
            finally { CryptographicOperations.ZeroMemory(passwordBytes); }
        }

        string? toAddress = PromptOptional("Recipient address");
        if (string.IsNullOrWhiteSpace(toAddress))
            return;

        ulong currentBalance = await active.GetBalanceAsync();
        decimal balanceAda = currentBalance / 1_000_000m;
        AnsiConsole.MarkupLine($"[grey]Available:[/] {balanceAda:0.000000} ADA");

        string? amountInput = PromptOptional("Amount (ADA)");
        if (string.IsNullOrWhiteSpace(amountInput))
            return;
        if (!decimal.TryParse(amountInput, out decimal ada) || ada <= 0)
        {
            AnsiConsole.MarkupLine("[red]Invalid amount.[/]");
            Pause();
            return;
        }
        ulong amount = (ulong)decimal.Round(ada * 1_000_000m, 0, MidpointRounding.AwayFromZero);

        TransactionRequest request = new(
            Recipients: [new TransactionRecipient(toAddress, amount)]
        );

        string txId = await active.SendAsync(request);
        AnsiConsole.MarkupLine($"[bold]Submitted:[/] {txId}");
        Pause();
    }

    private async Task SetActiveWalletAsync()
    {
        IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();
        if (wallets.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No wallets found.[/]");
            return;
        }

        string selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select wallet:")
            .AddChoices(wallets.Select(w => $"{w.Profile.Name} ({w.Id:N})")));

        string id = selected.Split('(').Last().TrimEnd(')');
        BurizaWallet wallet = wallets.First(w => w.Id.ToString("N") == id);
        await _walletManager.SetActiveAsync(wallet.Id);
        if (_activeWallet is not null)
            _appState.ClearWalletCache(_activeWallet.Id);
        _activeWallet?.Lock();
        _activeWallet = wallet;
        _lastBalanceLovelace = null;
        AnsiConsole.MarkupLine("[bold]Active wallet set.[/]");
        Pause();
    }

    private async Task SetNetworkAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        string choice = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select network:")
            .AddChoices(["Mainnet", "Preprod", "Preview"]));

        ChainInfo chainInfo = choice switch
        {
            "Preprod" => ChainRegistry.CardanoPreprod,
            "Preview" => ChainRegistry.CardanoPreview,
            _ => ChainRegistry.CardanoMainnet
        };

        _appState.ClearWalletCache(active.Id);
        await active.SetActiveChainAsync(chainInfo);
        _lastBalanceLovelace = null;
        await CacheActiveAddressAsync(active);
        AnsiConsole.MarkupLine("[bold]Network updated.[/]");
        Pause();
    }

    private async Task SetCustomProviderAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        string? endpoint = PromptOptional("Endpoint URL");
        if (string.IsNullOrWhiteSpace(endpoint))
            return;
        string apiKey = AnsiConsole.Ask("API Key (optional):", string.Empty);
        string? password = PromptOptional("Password", secret: true);
        if (string.IsNullOrWhiteSpace(password))
            return;

        ChainInfo chainInfo = ChainRegistry.Get(active.ActiveChain, active.Network);

        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            await active.SetCustomProviderConfigAsync(chainInfo, endpoint, string.IsNullOrWhiteSpace(apiKey) ? null : apiKey, passwordBytes);
            await active.LoadCustomProviderConfigAsync(chainInfo, passwordBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
        AnsiConsole.MarkupLine("[bold]Custom provider saved and loaded.[/]");
        Pause();
    }

    private async Task ClearCustomProviderAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        ChainInfo chainInfo = ChainRegistry.Get(active.ActiveChain, active.Network);

        await active.ClearCustomProviderConfigAsync(chainInfo);
        AnsiConsole.MarkupLine("[bold]Custom provider cleared.[/]");
        Pause();
    }

    private async Task ExportMnemonicAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();

        if (!active.IsUnlocked)
        {
            string? password = PromptOptional("Password", secret: true);
            if (string.IsNullOrWhiteSpace(password))
                return;

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            try { await active.UnlockAsync(passwordBytes); }
            finally { CryptographicOperations.ZeroMemory(passwordBytes); }
        }

        active.ExportMnemonic(span =>
        {
            AnsiConsole.Write(new Panel(new Markup($"[bold]Mnemonic:[/]\n\n[white]{span}[/]"))
                .BorderColor(Color.Grey)
                .RoundedBorder()
                .Padding(1, 0, 1, 0));
        });
        Pause();
    }

    // Fixed row positions in the frame layout
    private const int RowTip = 1;        // Block/Slot/Age + Address
    private const int RowChain = 2;      // Chain/Network + UTC time
    private const int RowBalanceLine = 11; // Balance + lock status
    private const int RowMenuTitle = 13; // Menu title
    private const int RowMenuFirst = 14; // First menu item

    // Previous-frame snapshots for dirty checking
    private string _prevTipLine = string.Empty;
    private string _prevBalanceLine = string.Empty;
    private long _lastClockTick;

    /// <summary>Build the tip/address line (row 1) content.</summary>
    private string BuildTipLine(int width)
    {
        BurizaWallet? active = _activeWallet;

        string blockInfo = Ansi.Grey("Connecting...");
        if (_lastTipSlot > 0)
        {
            string age = _lastTipAtUtc.HasValue
                ? Ansi.Grey($"({FormatAge(DateTime.UtcNow - _lastTipAtUtc.Value)})")
                : string.Empty;
            blockInfo = $"{Ansi.White($"Blk {_lastTipHeight} / Slot {_lastTipSlot}")} {age}";
        }

        string? address = active is not null ? GetCachedAddress(active) : null;
        string truncAddr = string.Empty;
        if (!string.IsNullOrEmpty(address) && address.Length > 23)
            truncAddr = Ansi.White($"{address[..15]}...{address[^8..]}");
        else if (!string.IsNullOrEmpty(address))
            truncAddr = Ansi.White(address);

        StringBuilder sb = new();
        if (!string.IsNullOrEmpty(truncAddr))
        {
            int gap = Math.Max(1, width - Ansi.StripLen(blockInfo) - Ansi.StripLen(truncAddr));
            sb.Append(blockInfo).Append(' ', gap).Append(truncAddr);
        }
        else
        {
            sb.Append(blockInfo);
        }
        return sb.ToString();
    }

    /// <summary>Build the chain/network + UTC time line (row 2) content.</summary>
    private string BuildChainLine(int width)
    {
        BurizaWallet? active = _activeWallet;
        string chainNetwork = active is not null
            ? $"{Ansi.Cyan(active.ActiveChain.ToString())} {Ansi.Grey("•")} {Ansi.Cyan(active.Network)}"
            : Ansi.Grey("No wallet");
        string utcTime = Ansi.Grey($"{DateTime.UtcNow:HH:mm:ss} UTC");
        int gap = Math.Max(1, width - Ansi.StripLen(chainNetwork) - Ansi.StripLen(utcTime));
        return $"{chainNetwork}{new string(' ', gap)}{utcTime}";
    }

    /// <summary>Build the balance + lock status line (row 11) content.</summary>
    private string BuildBalanceLine(int width)
    {
        BurizaWallet? active = _activeWallet;

        string balanceText = Ansi.Grey("Unavailable");
        if (active is not null && _lastBalanceLovelace.HasValue)
        {
            decimal ada = _lastBalanceLovelace.Value / 1_000_000m;
            balanceText = Ansi.Green($"{ada:0.000000} ADA");
        }

        string lockText = active switch
        {
            null => string.Empty,
            { IsUnlocked: true } => Ansi.Green("(unlocked)"),
            _ => Ansi.Yellow("(locked)")
        };

        StringBuilder sb = new();
        sb.Append("  ").Append(balanceText);
        if (!string.IsNullOrEmpty(lockText))
        {
            int gap = Math.Max(1, width - Ansi.StripLen(balanceText) - 2 - Ansi.StripLen(lockText));
            sb.Append(' ', gap).Append(lockText);
        }
        return sb.ToString();
    }

    /// <summary>Write content at a specific row without touching other rows.</summary>
    private static void WriteAtRow(int row, string content)
    {
        Console.Out.Write($"\x1b[{row};1H{content}\x1b[K");
    }

    /// <summary>Render a single menu item at its row.</summary>
    private static void RenderMenuItem(IReadOnlyList<string> items, int index, bool isSelected, int menuFirstRow)
    {
        Console.Out.Write(BuildMenuItemPatch(items, index, isSelected, menuFirstRow));
    }

    private static string BuildMenuItemPatch(IReadOnlyList<string> items, int index, bool isSelected, int menuFirstRow)
    {
        int row = menuFirstRow + index;
        string line = isSelected
            ? $"  {Ansi.BoldCyan(">")} {Ansi.Bold(items[index])}"
            : $"    {items[index]}";
        return $"\x1b[{row};1H{line}\x1b[K";
    }

    private static string BuildFullMenuPatch(IReadOnlyList<string> items, int selected, int menuFirstRow)
    {
        StringBuilder sb = new();
        for (int i = 0; i < items.Count; i++)
            sb.Append(BuildMenuItemPatch(items, i, i == selected, menuFirstRow));
        return sb.ToString();
    }

    /// <summary>Draw the full frame once (first render only).</summary>
    private async Task RenderFullFrameAsync(string title, IReadOnlyList<string> items, int selected)
    {
        BurizaWallet? active = _activeWallet;
        int width = Math.Max(Console.WindowWidth, 60);

        if (active is not null)
            EnsureHeartbeat(active);

        if (active is not null && !_lastBalanceLovelace.HasValue)
            await RefreshBalanceAsync(active);

        StringBuilder fb = new();

        void pos(int row) => fb.Append($"\x1b[{row};1H");

        // Row 1: Tip
        _prevTipLine = BuildTipLine(width);
        pos(RowTip); fb.Append(_prevTipLine).Append("\x1b[K");

        // Row 2: Chain
        pos(RowChain); fb.Append(BuildChainLine(width)).Append("\x1b[K");

        // Row 3: blank
        pos(3); fb.Append("\x1b[K");

        // Row 4-8: logo
        string[] logo =
        [
            @"    ____  ",
            @"   | __ ) ",
            @"   |  _ \ ",
            @"   | |_) |",
            @"   |____/ "
        ];
        for (int l = 0; l < logo.Length; l++)
        {
            pos(4 + l);
            int pad = Math.Max(0, (width - logo[l].Length) / 2);
            fb.Append(' ', pad).Append(Ansi.BoldCyan(logo[l])).Append("\x1b[K");
        }

        // Row 9: brand
        const string brandText = "B U R I Z A";
        int brandPad = Math.Max(0, (width - brandText.Length) / 2);
        pos(9); fb.Append(' ', brandPad).Append(Ansi.BoldWhite(brandText)).Append("\x1b[K");

        // Row 10: blank
        pos(10); fb.Append("\x1b[K");

        // Row 11: balance
        _prevBalanceLine = BuildBalanceLine(width);
        pos(RowBalanceLine); fb.Append(_prevBalanceLine).Append("\x1b[K");

        // Row 12: blank
        pos(12); fb.Append("\x1b[K");

        // Row 13: menu title
        pos(RowMenuTitle); fb.Append("  ").Append(Ansi.Grey(title)).Append("\x1b[K");

        // Row 14+: menu items
        fb.Append(BuildFullMenuPatch(items, selected, RowMenuFirst));

        // Clear below
        pos(RowMenuFirst + items.Count); fb.Append("\x1b[J");

        Console.Out.Write(fb);
    }

    /// <summary>Update only the data lines that changed (tip, chain, balance). No menu redraw.</summary>
    private void UpdateDataLines()
    {
        int width = Math.Max(Console.WindowWidth, 60);
        StringBuilder patch = new();

        string tipLine = BuildTipLine(width);
        if (tipLine != _prevTipLine)
        {
            _prevTipLine = tipLine;
            patch.Append($"\x1b[{RowTip};1H").Append(tipLine).Append("\x1b[K");
        }

        string chainLine = BuildChainLine(width);
        // Chain line always has UTC time that changes every second — always update
        patch.Append($"\x1b[{RowChain};1H").Append(chainLine).Append("\x1b[K");

        string balanceLine = BuildBalanceLine(width);
        if (balanceLine != _prevBalanceLine)
        {
            _prevBalanceLine = balanceLine;
            patch.Append($"\x1b[{RowBalanceLine};1H").Append(balanceLine).Append("\x1b[K");
        }

        if (patch.Length > 0)
            Console.Out.Write(patch);
    }

    /// <summary>Raw ANSI escape helpers — no Spectre dependency.</summary>
    private static class Ansi
    {
        public static string Grey(string s) => $"\x1b[90m{s}\x1b[0m";
        public static string White(string s) => $"\x1b[97m{s}\x1b[0m";
        public static string Cyan(string s) => $"\x1b[36m{s}\x1b[0m";
        public static string Green(string s) => $"\x1b[32m{s}\x1b[0m";
        public static string Yellow(string s) => $"\x1b[33m{s}\x1b[0m";
        public static string Bold(string s) => $"\x1b[1m{s}\x1b[0m";
        public static string BoldCyan(string s) => $"\x1b[1;36m{s}\x1b[0m";
        public static string BoldWhite(string s) => $"\x1b[1;97m{s}\x1b[0m";

        /// <summary>Returns the visible character length of a string (strips ANSI sequences).</summary>
        public static int StripLen(string s)
        {
            int len = 0;
            bool inEsc = false;
            foreach (char c in s)
            {
                if (c == '\x1b') { inEsc = true; continue; }
                if (inEsc) { if (c is >= 'A' and <= 'Z' or >= 'a' and <= 'z') inEsc = false; continue; }
                len++;
            }
            return len;
        }
    }

    private async Task<string> RunMenuLoopAsync(string title, IReadOnlyList<string> items)
    {
        int selected = 0;
        bool firstRender = true;
        _lastClockTick = Environment.TickCount64;

        while (true)
        {
            if (firstRender)
            {
                // Alternate screen, hide cursor, clear
                Console.Write("\x1b[?1049h\x1b[?25l\x1b[2J\x1b[H");
                await RenderFullFrameAsync(title, items, selected);
                firstRender = false;
                _needsRender = false;
            }
            else if (_needsRender || Environment.TickCount64 - _lastClockTick >= 1000)
            {
                UpdateDataLines();
                _needsRender = false;
                _lastClockTick = Environment.TickCount64;
            }

            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        selected = selected <= 0 ? items.Count - 1 : selected - 1;
                        Console.Out.Write(BuildFullMenuPatch(items, selected, RowMenuFirst));
                        break;
                    case ConsoleKey.DownArrow:
                        selected = selected >= items.Count - 1 ? 0 : selected + 1;
                        Console.Out.Write(BuildFullMenuPatch(items, selected, RowMenuFirst));
                        break;
                    case ConsoleKey.Enter:
                        Console.Write("\x1b[?25h\x1b[?1049l");
                        return items[selected];
                    case ConsoleKey.Escape:
                        Console.Write("\x1b[?25h\x1b[?1049l");
                        if (items.Contains("Back"))
                            return "Back";
                        if (items.Contains("Exit"))
                            return "Exit";
                        break;
                }
            }
            else
            {
                await Task.Delay(100);
            }
        }
    }



    private async Task ExecuteWithHandlingAsync(Func<Task> action)
    {
        Console.Clear();
        try
        {
            await action();
        }
        catch (CryptographicException)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Invalid password or PIN.");
            Pause();
        }
        catch (Grpc.Core.RpcException)
        {
            AnsiConsole.MarkupLine("[red]Network error:[/] Provider is not configured or unreachable.");
            string hint = await BuildProviderHintAsync();
            if (!string.IsNullOrEmpty(hint))
                AnsiConsole.MarkupLine(hint);
            AnsiConsole.MarkupLine("[grey]Use 'Set custom provider' or edit appsettings.json.[/]");
            Pause();
        }
        catch (HttpRequestException)
        {
            AnsiConsole.MarkupLine("[red]Network error:[/] Provider is not configured or unreachable.");
            string hint = await BuildProviderHintAsync();
            if (!string.IsNullOrEmpty(hint))
                AnsiConsole.MarkupLine(hint);
            AnsiConsole.MarkupLine("[grey]Use 'Set custom provider' or edit appsettings.json.[/]");
            Pause();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            Pause();
        }
    }

    private static void Pause()
    {
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }

    private static string? PromptOptional(string label, bool secret = false)
    {
        TextPrompt<string> prompt = new($"[grey]{label}[/] [dim](enter to cancel)[/]:");
        prompt.AllowEmpty();
        if (secret)
            prompt.Secret();
        string value = AnsiConsole.Prompt(prompt);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? PromptAndConfirmPassword(string label)
    {
        while (true)
        {
            string? password = PromptOptional(label, secret: true);
            if (string.IsNullOrWhiteSpace(password))
                return null;

            string? confirm = PromptOptional("Confirm password", secret: true);
            if (string.IsNullOrWhiteSpace(confirm))
                return null;

            if (password == confirm)
                return password;

            AnsiConsole.MarkupLine("[red]Passwords do not match.[/] Please try again.");
        }
    }

    private async Task<string?> ResolveProviderEndpointAsync(BurizaWallet active)
    {
        ChainInfo chainInfo = ChainRegistry.Get(active.ActiveChain, active.Network);

        CustomProviderConfig? custom = await active.GetCustomProviderConfigAsync(chainInfo);
        if (!string.IsNullOrWhiteSpace(custom?.Endpoint))
            return custom.Endpoint;

        if (chainInfo.Chain != ChainType.Cardano)
            return null;

        CardanoSettings? cardano = _settings.Cardano;
        if (cardano == null) return null;

        return chainInfo.Network switch
        {
            "preprod" => cardano.PreprodEndpoint,
            "preview" => cardano.PreviewEndpoint,
            _ => cardano.MainnetEndpoint
        };
    }

    private void EnsureHeartbeat(BurizaWallet active)
    {
        string key = $"{active.ActiveChain}:{active.Network}";
        if (_heartbeat != null && _heartbeatKey == key)
            return;

        _heartbeat?.Dispose();
        _heartbeatProvider?.Dispose();
        _heartbeatKey = key;
        _lastTipSlot = 0;
        _lastTipHeight = 0;
        _lastTipHash = string.Empty;
        _lastTipAtUtc = null;

        ChainInfo chainInfo = ChainRegistry.Get(active.ActiveChain, active.Network);
        _heartbeatProvider = _providerFactory.CreateProvider(chainInfo);
        _heartbeat = new HeartbeatService(_heartbeatProvider);
        _heartbeat.Beat += (_, _) =>
        {
            _lastTipSlot = _heartbeat.Slot;
            _lastTipHeight = _heartbeat.Height;
            _lastTipHash = _heartbeat.Hash;
            _lastTipAtUtc = DateTime.UtcNow;
            if (_activeWallet != null)
                _ = RefreshBalanceAsync(_activeWallet);
            _needsRender = true;
        };
    }

    private async Task RefreshBalanceAsync(BurizaWallet active)
    {
        if (_balanceRefreshing)
            return;

        _balanceRefreshing = true;
        try
        {
            ulong balance = await active.GetBalanceAsync();
            _lastBalanceLovelace = balance;
        }
        catch
        {
            // Ignore provider failures; keep last balance if any.
        }
        finally
        {
            _balanceRefreshing = false;
        }
    }

    private static string FormatAge(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 60)
            return $"{(int)elapsed.TotalSeconds}s ago";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes}m ago";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours}h ago";
        return $"{(int)elapsed.TotalDays}d ago";
    }

    private async Task<string> BuildProviderHintAsync()
    {
        if (_activeWallet is null) return string.Empty;

        string? endpoint = await ResolveProviderEndpointAsync(_activeWallet);
        if (string.IsNullOrWhiteSpace(endpoint))
            return "[grey]Active provider: not configured[/]";

        return $"[grey]Active provider:[/] {endpoint}";
    }

    private Task<BurizaWallet> RequireActiveWalletAsync()
    {
        if (_activeWallet is not null)
            return Task.FromResult(_activeWallet);

        throw new InvalidOperationException("No active wallet. Create or import a wallet first.");
    }

    private string? GetCachedAddress(BurizaWallet wallet)
    {
        ChainInfo chainInfo = ChainRegistry.Get(wallet.ActiveChain, wallet.Network);
        return _appState.GetCachedAddress(wallet.Id, chainInfo, wallet.ActiveAccountIndex, 0, false);
    }

    private async Task CacheActiveAddressAsync(BurizaWallet wallet)
    {
        ChainAddressData? data = await wallet.GetAddressInfoAsync();
        if (data is null) return;

        ChainInfo chainInfo = ChainRegistry.Get(wallet.ActiveChain, wallet.Network);
        _appState.CacheAddress(wallet.Id, chainInfo, wallet.ActiveAccountIndex, 0, false, data.Address);
    }
}
