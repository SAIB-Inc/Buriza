using Buriza.Cli;
using Buriza.Cli.Ui;
using Buriza.Core.Interfaces;
using Buriza.Core.Interfaces.Wallet;
using Buriza.Data.Models;
using Microsoft.Extensions.DependencyInjection;

AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

ServiceProvider services = CliBootstrap.ConfigureServices();
CliShell shell = new(
    services.GetRequiredService<IWalletManager>(),
    services.GetRequiredService<ChainProviderSettings>(),
    services.GetRequiredService<IBurizaChainProviderFactory>());

await shell.RunAsync();
