using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Collections;
using Buriza.Core.Models.Chain;
using Buriza.Core.Models.Config;
using Buriza.Core.Models.Enums;
using Buriza.Core.Models.Transaction;
using Buriza.Core.Models.Wallet;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Core.Services;
using Buriza.Data.Models;
using Chrysalis.Cbor.Types.Cardano.Core.Transaction;
using Chrysalis.Network.Cbor.LocalStateQuery;
using Spectre.Console;
using Buriza.Core.Interfaces;

namespace Buriza.Cli.Ui;

public sealed class CliShell(IWalletManager walletManager, ChainProviderSettings settings, IBurizaChainProviderFactory providerFactory)
{
    private readonly IWalletManager _walletManager = walletManager;
    private readonly ChainProviderSettings _settings = settings;
    private readonly IBurizaChainProviderFactory _providerFactory = providerFactory;
    private HeartbeatService? _heartbeat;
    private string? _heartbeatKey;
    private ulong _lastTipSlot;
    private string _lastTipHash = string.Empty;
    private DateTime? _lastTipAtUtc;
    private ulong? _lastBalanceLovelace;
    private bool _balanceRefreshing;
    private bool _needsRender;

    public async Task RunAsync()
    {
        while (true)
        {
            IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();
            bool hasWallets = wallets.Count > 0;
            BurizaWallet? active = await _walletManager.GetActiveAsync();
            bool hasActive = active is not null;

            if (!hasWallets || !hasActive)
            {
                string startChoice = await RunMenuLoopAsync(
                    "Get started",
                    ["Create wallet", "Import wallet", "Exit"]);

                switch (startChoice)
                {
                    case "Create wallet":
                        await ExecuteWithHandlingAsync(() => CreateWalletAsync());
                        break;
                    case "Import wallet":
                        await ExecuteWithHandlingAsync(() => ImportWalletAsync());
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
            string choice = await RunMenuLoopAsync(
                "Wallets",
                ["List wallets", "Set active wallet", "Export mnemonic", "Create wallet", "Import wallet", "Back"]);

            switch (choice)
            {
                case "List wallets":
                    await ExecuteWithHandlingAsync(() => ListWalletsAsync());
                    break;
                case "Set active wallet":
                    await ExecuteWithHandlingAsync(() => SetActiveWalletAsync());
                    break;
                case "Export mnemonic":
                    await ExecuteWithHandlingAsync(() => ExportMnemonicAsync());
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
                ["Show receive address", "Show balance", "Show assets", "Show UTXOs", "Show transaction history", "Back"]);

            switch (choice)
            {
                case "Show receive address":
                    await ExecuteWithHandlingAsync(() => ShowReceiveAddressAsync());
                    break;
                case "Show balance":
                    await ExecuteWithHandlingAsync(() => ShowBalanceAsync());
                    break;
                case "Show assets":
                    await ExecuteWithHandlingAsync(() => ShowAssetsAsync());
                    break;
                case "Show UTXOs":
                    await ExecuteWithHandlingAsync(() => ShowUtxosAsync());
                    break;
                case "Show transaction history":
                    await ExecuteWithHandlingAsync(() => ShowTransactionHistoryAsync());
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
                    await ExecuteWithHandlingAsync(() => SendTransactionAsync());
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
                    await ExecuteWithHandlingAsync(() => SetNetworkAsync());
                    break;
                case "Set custom provider":
                    await ExecuteWithHandlingAsync(() => SetCustomProviderAsync());
                    break;
                case "Clear custom provider":
                    await ExecuteWithHandlingAsync(() => ClearCustomProviderAsync());
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
                    await ExecuteWithHandlingAsync(() => ShowProtocolParametersAsync());
                    break;
                case "Back":
                    return;
            }
        }
    }

    private async Task ShowProtocolParametersAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        ProtocolParams parameters = await active.GetProtocolParametersAsync();

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

    private static object NormalizeDictionary(IDictionary dictionary, int depth)
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
        _walletManager.GenerateMnemonic(24, span =>
        {
            mnemonic = span.ToString();
        });

        AnsiConsole.Write(new Panel(new Markup($"[bold]Write down your mnemonic:[/]\n\n[white]{mnemonic}[/]"))
            .BorderColor(Color.Grey)
            .RoundedBorder()
            .Padding(1, 0, 1, 0));

        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync(name, mnemonic, password);
        await _walletManager.SetActiveChainAsync(wallet.Id, ChainRegistry.CardanoPreview, null);
        await _walletManager.SetActiveAsync(wallet.Id);
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

        BurizaWallet wallet = await _walletManager.CreateFromMnemonicAsync(name, mnemonic, password);
        await _walletManager.SetActiveChainAsync(wallet.Id, ChainRegistry.CardanoPreview, null);
        await _walletManager.SetActiveAsync(wallet.Id);
        AnsiConsole.MarkupLine("[bold]Wallet imported and set active.[/]");
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

        BurizaWallet? active = await _walletManager.GetActiveAsync();
        foreach (BurizaWallet wallet in wallets)
        {
            table.AddRow(
                wallet.Id.ToString("N"),
                wallet.Profile.Name,
                wallet.Network.ToString(),
                active?.Id == wallet.Id ? "Yes" : "No");
        }

        AnsiConsole.Write(table);
        Pause();
    }

    private async Task ShowReceiveAddressAsync()
    {
        IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();
        if (wallets.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No wallets found.[/]");
            return;
        }

        BurizaWallet active = await ResolveActiveWalletAsync(wallets);
        string address = await _walletManager.GetReceiveAddressAsync(active.Id);
        AnsiConsole.MarkupLine($"[bold]Receive address:[/] {address}");
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

    private async Task ShowTransactionHistoryAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        IReadOnlyList<TransactionHistory> history = await active.GetTransactionHistoryAsync();
        if (history.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No transactions found.[/]");
            Pause();
            return;
        }

        Table table = new();
        table.AddColumn("TxId");
        table.AddColumn("Type");
        table.AddColumn("From");
        table.AddColumn("To");
        table.AddColumn("Timestamp");
        foreach (TransactionHistory tx in history)
            table.AddRow(tx.TransactionId, tx.Type.ToString(), tx.FromAddress, tx.ToAddress, tx.Timestamp.ToString("u"));
        AnsiConsole.Write(table);
        Pause();
    }

    private async Task SendTransactionAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
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

        string? password = PromptOptional("Password", secret: true);
        if (string.IsNullOrWhiteSpace(password))
            return;

        UnsignedTransaction unsignedTx = await active.BuildTransactionAsync(amount, toAddress);
        Transaction signed = await _walletManager.SignTransactionAsync(active.Id, active.ActiveAccountIndex, 0, unsignedTx, password);
        string txId = await active.SubmitAsync(signed);
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

        await _walletManager.SetActiveChainAsync(active.Id, chainInfo, null);
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

        ChainInfo chainInfo = active.Network switch
        {
            NetworkType.Preprod => ChainRegistry.CardanoPreprod,
            NetworkType.Preview => ChainRegistry.CardanoPreview,
            _ => ChainRegistry.CardanoMainnet
        };

        await _walletManager.SetCustomProviderConfigAsync(chainInfo, endpoint, string.IsNullOrWhiteSpace(apiKey) ? null : apiKey, password);
        await _walletManager.LoadCustomProviderConfigAsync(chainInfo, password);
        AnsiConsole.MarkupLine("[bold]Custom provider saved and loaded.[/]");
        Pause();
    }

    private async Task ClearCustomProviderAsync()
    {
        BurizaWallet active = await RequireActiveWalletAsync();
        ChainInfo chainInfo = active.Network switch
        {
            NetworkType.Preprod => ChainRegistry.CardanoPreprod,
            NetworkType.Preview => ChainRegistry.CardanoPreview,
            _ => ChainRegistry.CardanoMainnet
        };

        await _walletManager.ClearCustomProviderConfigAsync(chainInfo);
        AnsiConsole.MarkupLine("[bold]Custom provider cleared.[/]");
        Pause();
    }

    private async Task ExportMnemonicAsync()
    {
        IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();
        if (wallets.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No wallets found.[/]");
            return;
        }

        BurizaWallet active = await ResolveActiveWalletAsync(wallets);

        string password = AnsiConsole.Prompt(new TextPrompt<string>("Password:").Secret());
        await _walletManager.ExportMnemonicAsync(active.Id, password, span =>
        {
        AnsiConsole.Write(new Panel(new Markup($"[bold]Mnemonic:[/]\n\n[white]{span.ToString()}[/]"))
            .BorderColor(Color.Grey)
            .RoundedBorder()
            .Padding(1, 0, 1, 0));
        });
        Pause();
    }

    private async Task RenderHeaderAsync()
    {
        BurizaWallet? active = await _walletManager.GetActiveAsync();
        string walletValue = active is null
            ? "[grey]No active wallet[/]"
            : $"[bold]{active.Profile.Name}[/] • {active.ActiveChain} • {active.Network}";

        string? address = active?.GetAddressInfo()?.ReceiveAddress;
        string addressValue = string.IsNullOrEmpty(address)
            ? "[grey]Not derived[/]"
            : address;

        string providerValue = "[grey]Not configured[/]";
        if (active is not null)
        {
            string? endpoint = await ResolveProviderEndpointAsync(active);
            if (!string.IsNullOrWhiteSpace(endpoint))
                providerValue = Markup.Escape(endpoint);
        }

        string balanceValue = "[grey]Unavailable[/]";
        if (active is not null)
        {
            if (!_lastBalanceLovelace.HasValue)
                await RefreshBalanceAsync(active);

            if (_lastBalanceLovelace.HasValue)
            {
                decimal ada = _lastBalanceLovelace.Value / 1_000_000m;
                balanceValue = $"[green]{ada:0.000000} ADA[/]";
            }
        }

        string tipValue = "[grey]Unavailable[/]";
        if (active is not null)
        {
            EnsureHeartbeat(active);
            if (_lastTipSlot > 0)
            {
                string hash = string.IsNullOrWhiteSpace(_lastTipHash)
                    ? "-"
                    : $"{_lastTipHash[..Math.Min(12, _lastTipHash.Length)]}…";
                string age = _lastTipAtUtc.HasValue
                    ? $" • {FormatAge(DateTime.UtcNow - _lastTipAtUtc.Value)}"
                    : string.Empty;
                tipValue = $"slot {_lastTipSlot} • {hash}{age}";
            }
        }

        Grid grid = new();
        grid.AddColumn(new GridColumn().NoWrap());
        grid.AddColumn(new GridColumn());

        grid.AddRow(new Markup("[grey]Wallet[/]"), new Markup(walletValue));
        grid.AddRow(new Markup("[grey]Balance[/]"), new Markup(balanceValue));
        grid.AddRow(new Markup("[grey]Address[/]"), new Markup(addressValue));
        grid.AddRow(new Markup("[grey]Provider[/]"), new Markup(providerValue));
        grid.AddRow(new Markup("[grey]Tip[/]"), new Markup(tipValue));

        Panel panel = new Panel(grid)
            .RoundedBorder()
            .BorderColor(Color.Teal)
            .Padding(1, 0, 1, 0);
        panel.Header = new PanelHeader(" [bold cyan]Buriza CLI[/] ", Justify.Center);
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private async Task<string> RunMenuLoopAsync(string title, IReadOnlyList<string> items)
    {
        int selected = 0;
        _needsRender = true;
        DateTime lastRenderAt = DateTime.MinValue;

        while (true)
        {
            if (_needsRender || DateTime.UtcNow - lastRenderAt > TimeSpan.FromSeconds(1))
            {
                AnsiConsole.Clear();
                await RenderHeaderAsync();
                RenderMenu(title, items, selected);
                _needsRender = false;
                lastRenderAt = DateTime.UtcNow;
            }

            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.UpArrow:
                        selected = selected <= 0 ? items.Count - 1 : selected - 1;
                        _needsRender = true;
                        break;
                    case ConsoleKey.DownArrow:
                        selected = selected >= items.Count - 1 ? 0 : selected + 1;
                        _needsRender = true;
                        break;
                    case ConsoleKey.Enter:
                        return items[selected];
                    case ConsoleKey.Escape:
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

    private static void RenderMenu(string title, IReadOnlyList<string> items, int selected)
    {
        Grid menu = new();
        menu.AddColumn(new GridColumn().NoWrap());
        menu.AddColumn(new GridColumn());

        for (int i = 0; i < items.Count; i++)
        {
            string prefix = i == selected ? "[bold cyan]>[/]" : " ";
            string label = i == selected ? $"[bold]{items[i]}[/]" : items[i];
            menu.AddRow(new Markup(prefix), new Markup(label));
        }

        Rows content = new(
            new Markup($"[grey]{title}[/]"),
            menu);

        Panel panel = new Panel(content)
            .RoundedBorder()
            .BorderColor(Color.Grey)
            .Padding(1, 0, 1, 0);

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }


    private async Task ExecuteWithHandlingAsync(Func<Task> action)
    {
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

        CustomProviderConfig? custom = await _walletManager.GetCustomProviderConfigAsync(chainInfo);
        if (!string.IsNullOrWhiteSpace(custom?.Endpoint))
            return custom.Endpoint;

        if (chainInfo.Chain != ChainType.Cardano)
            return null;

        CardanoSettings? cardano = _settings.Cardano;
        if (cardano == null) return null;

        return chainInfo.Network switch
        {
            NetworkType.Preprod => cardano.PreprodEndpoint,
            NetworkType.Preview => cardano.PreviewEndpoint,
            _ => cardano.MainnetEndpoint
        };
    }

    private void EnsureHeartbeat(BurizaWallet active)
    {
        string key = $"{active.ActiveChain}:{active.Network}";
        if (_heartbeat != null && _heartbeatKey == key)
            return;

        _heartbeat?.Dispose();
        _heartbeatKey = key;
        _lastTipSlot = 0;
        _lastTipHash = string.Empty;
        _lastTipAtUtc = null;

        ChainInfo chainInfo = ChainRegistry.Get(active.ActiveChain, active.Network);
        var provider = _providerFactory.CreateProvider(chainInfo);
        _heartbeat = new HeartbeatService(provider);
        _heartbeat.Beat += (_, _) =>
        {
            _lastTipSlot = _heartbeat.Slot;
            _lastTipHash = _heartbeat.Hash;
            _lastTipAtUtc = DateTime.UtcNow;
            BurizaWallet? current = _walletManager.GetActiveAsync().GetAwaiter().GetResult();
            if (current != null)
                _ = RefreshBalanceAsync(current);
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
        BurizaWallet? active = await _walletManager.GetActiveAsync();
        if (active is null) return string.Empty;

        string? endpoint = await ResolveProviderEndpointAsync(active);
        if (string.IsNullOrWhiteSpace(endpoint))
            return "[grey]Active provider: not configured[/]";

        return $"[grey]Active provider:[/] {endpoint}";
    }

    private async Task<BurizaWallet> RequireActiveWalletAsync()
    {
        IReadOnlyList<BurizaWallet> wallets = await _walletManager.GetAllAsync();
        if (wallets.Count == 0)
            throw new InvalidOperationException("No wallets found.");

        return await ResolveActiveWalletAsync(wallets);
    }

    private async Task<BurizaWallet> ResolveActiveWalletAsync(IReadOnlyList<BurizaWallet> wallets)
    {
        BurizaWallet? active = await _walletManager.GetActiveAsync();
        if (active is not null)
            return active;

        string selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
            .Title("Select wallet:")
            .AddChoices(wallets.Select(w => $"{w.Profile.Name} ({w.Id:N})")));

        string id = selected.Split('(').Last().TrimEnd(')');
        BurizaWallet wallet = wallets.First(w => w.Id.ToString("N") == id);
        await _walletManager.SetActiveAsync(wallet.Id);
        return wallet;
    }
}
