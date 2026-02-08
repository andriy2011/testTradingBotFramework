// ============================================================================
// File: OandaSettings.cs
// Purpose: Configuration POCO for the Oanda v20 REST / Streaming API.
// Binding: Bound from the "Oanda" section of appsettings.json via
//          IOptions<OandaSettings> in the DI container.
// Notes: Provides computed read-only properties (RestBaseUrl, StreamBaseUrl)
//        that automatically resolve to the correct Oanda hostname based on
//        whether the practice (demo) or live environment is selected.
//        UsePractice defaults to true so new deployments never accidentally
//        trade against a live Oanda account.
// ============================================================================

namespace testTradingBotFramework.Configuration;

/// <summary>
/// Holds the Oanda API credentials, account identifier, and computed base URLs
/// for both REST and streaming endpoints. The practice (demo) vs. live
/// environment is toggled by <see cref="UsePractice"/>, which also controls
/// which Oanda hostnames are returned by the URL properties.
/// </summary>
public class OandaSettings
{
    /// <summary>
    /// The configuration section name used to bind this settings class.
    /// Referenced during service registration (e.g.,
    /// <c>builder.Services.Configure&lt;OandaSettings&gt;(config.GetSection(OandaSettings.SectionName))</c>).
    /// </summary>
    public const string SectionName = "Oanda";

    /// <summary>
    /// The Oanda personal access token (bearer token) used to authenticate
    /// all API requests. Generated from the Oanda account management portal.
    /// Must be kept confidential -- never log or expose this value.
    /// </summary>
    public string ApiToken { get; set; } = string.Empty;

    /// <summary>
    /// The Oanda account identifier (e.g., "101-004-XXXXXXX-001") that
    /// specifies which trading account the bot operates against.
    /// </summary>
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c>, the bot connects to the Oanda Practice (demo)
    /// environment; when <c>false</c>, it connects to the live trading
    /// environment. Defaults to <c>true</c> as a safety measure to prevent
    /// accidental real-money trades during development and testing.
    /// </summary>
    public bool UsePractice { get; set; } = true;

    /// <summary>
    /// Computed REST API base URL. Resolves to the practice hostname
    /// (<c>api-fxpractice.oanda.com</c>) or the live hostname
    /// (<c>api-fxtrade.oanda.com</c>) depending on <see cref="UsePractice"/>.
    /// Used by HTTP clients for order placement, account queries, etc.
    /// </summary>
    public string RestBaseUrl => UsePractice
        ? "https://api-fxpractice.oanda.com"
        : "https://api-fxtrade.oanda.com";

    /// <summary>
    /// Computed Streaming API base URL. Resolves to the practice hostname
    /// (<c>stream-fxpractice.oanda.com</c>) or the live hostname
    /// (<c>stream-fxtrade.oanda.com</c>) depending on <see cref="UsePractice"/>.
    /// Used by streaming clients for real-time price and transaction feeds.
    /// </summary>
    public string StreamBaseUrl => UsePractice
        ? "https://stream-fxpractice.oanda.com"
        : "https://stream-fxtrade.oanda.com";
}
