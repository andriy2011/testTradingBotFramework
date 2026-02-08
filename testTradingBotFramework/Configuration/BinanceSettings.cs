// ============================================================================
// File: BinanceSettings.cs
// Purpose: Configuration POCO for Binance exchange API connectivity.
// Binding: Bound from the "Binance" section of appsettings.json via
//          IOptions<BinanceSettings> in the DI container.
// Notes: UseTestnet defaults to true so that new deployments never
//        accidentally trade against the live Binance exchange.
// ============================================================================

namespace testTradingBotFramework.Configuration;

/// <summary>
/// Holds the Binance exchange API credentials and environment selection.
/// This class is populated automatically by the .NET configuration system
/// from the "Binance" section in appsettings.json (or environment variables,
/// user secrets, etc.) and injected wherever <c>IOptions&lt;BinanceSettings&gt;</c>
/// is requested.
/// </summary>
public class BinanceSettings
{
    /// <summary>
    /// The configuration section name used to bind this settings class.
    /// Referenced during service registration (e.g.,
    /// <c>builder.Services.Configure&lt;BinanceSettings&gt;(config.GetSection(BinanceSettings.SectionName))</c>).
    /// </summary>
    public const string SectionName = "Binance";

    /// <summary>
    /// The Binance API key issued from the Binance dashboard.
    /// Required for authenticating REST and WebSocket requests.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The Binance API secret used to sign requests (HMAC-SHA256).
    /// Must be kept confidential -- never log or expose this value.
    /// </summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c>, the bot connects to the Binance Testnet environment
    /// (testnet.binance.vision) instead of the live production exchange.
    /// Defaults to <c>true</c> as a safety measure to prevent accidental
    /// real-money trades during development and testing.
    /// </summary>
    public bool UseTestnet { get; set; } = true;
}
