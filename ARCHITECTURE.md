# Trading Bot Framework — Architecture & Execution Flow

## High-Level Overview

```
                        +-------------------+
                        |    Program.cs     |
                        | (Entry Point)     |
                        +--------+----------+
                                 |
                    Host.CreateDefaultBuilder()
                      .UseTradingBotSerilog()
                      .AddTradingBotServices()
                                 |
                    +------------+-------------+
                    |   .NET Generic Host      |
                    |  (runs 6 BackgroundServices concurrently)
                    +---+---+---+---+---+------+
                        |   |   |   |   |
          +-------------+   |   |   |   +------------------+
          |         +-------+   |   +--------+              |
          v         v           v            v              v
  TradingBot   EventHub    PriceMonitor  PositionSync  AccountSync  Dashboard
   Worker      Listener      Worker       Worker        Worker      Worker
```

---

## Startup Sequence

### 1. Program.cs
```
Program.cs
  --> Host.CreateDefaultBuilder(args)
        --> Loads appsettings.json, environment variables
        --> .UseTradingBotSerilog()
              --> Configures Serilog: File sink (daily rolling, 30-day retention)
                                     Console sink (Information+)
        --> .ConfigureServices()
              --> AddTradingBotServices() [DI Composition Root]
  --> host.Build()
  --> host.RunAsync()  // blocks until shutdown
```

### 2. DI Composition Root (ServiceCollectionExtensions.cs)

```
AddTradingBotServices()
  |
  |-- IOptions<T> Binding (from appsettings.json)
  |     BinanceSettings   <-- "Binance" section
  |     OandaSettings     <-- "Oanda" section
  |     EventHubSettings  <-- "EventHub" section
  |     TradingSettings   <-- "Trading" section
  |
  |-- Exchange Clients (Keyed DI by ExchangeName)
  |     IBinanceRestClient   --> BinanceRestClient (singleton, testnet/live)
  |     IBinanceSocketClient --> BinanceSocketClient (singleton, WebSocket)
  |     OandaApiClient       --> HttpClient (via AddHttpClient<T>)
  |     IExchangeClient[Binance] --> BinanceExchangeClient
  |     IExchangeClient[Oanda]   --> OandaExchangeClient
  |     IExchangeFactory         --> ExchangeFactory (wraps keyed DI)
  |
  |-- Price Monitors (Keyed DI by ExchangeName)
  |     IPriceMonitor[Binance] --> BinancePriceMonitor (WebSocket)
  |     IPriceMonitor[Oanda]   --> OandaPriceMonitor (SSE stream)
  |
  |-- Core Business Services (all singletons)
  |     IPositionManager   --> PositionManager (ConcurrentDictionary)
  |     IPositionSizer     --> FixedFractionPositionSizer
  |     ITradeHistoryStore --> InMemoryTradeHistoryStore (ConcurrentBag)
  |     IAccountingService --> AccountingService
  |     IOrderManager      --> OrderManager
  |     ISignalParser      --> SignalParser (System.Text.Json)
  |     ISignalDispatcher  --> SignalDispatcher
  |
  |-- Dashboard
  |     DashboardRenderer (manual factory for keyed IPriceMonitor injection)
  |
  |-- Background Workers (6 IHostedService)
        TradingBotWorker        --> Health checks + heartbeat
        EventHubListenerService --> Signal ingestion
        PriceMonitorWorker      --> Price feed management
        PositionSyncWorker      --> Position reconciliation
        AccountSyncWorker       --> Balance sync + P&L reconciliation
        DashboardWorker         --> Console UI rendering
```

---

## Core Execution Flows

### Flow 1: Signal Processing Pipeline (Main Trading Flow)

This is the primary path from receiving an external trade signal to executing an order.

```
Azure Event Hub
      |
      v
EventHubListenerService (BackgroundService)
      |  ProcessEventAsync()
      |  1. Decode UTF-8 event body
      |  2. Parse JSON via SignalParser
      |  3. Dispatch via SignalDispatcher
      |  4. Checkpoint the event
      |
      v
SignalParser.Parse(rawJson)
      |  - Deserialize JSON -> TradeSignal
      |  - JsonStringEnumConverter for enum fields
      |  - Return null if: malformed JSON, empty, missing Symbol
      |
      v
SignalDispatcher.DispatchAsync(signal)
      |  - Log the signal
      |  - Delegate to OrderManager
      |  - Catch exceptions (don't crash Event Hub listener)
      |
      v
OrderManager.ExecuteSignalAsync(signal)
      |
      |  STEP 1: Risk Check
      |  PositionManager.ValidateRiskLimits(exchange)
      |  --> SKIP if open positions >= MaxOpenPositions
      |
      |  STEP 2: Resolve Exchange Client
      |  ExchangeFactory.GetClient(exchange)
      |  --> Keyed DI: Binance -> BinanceExchangeClient
      |                Oanda   -> OandaExchangeClient
      |
      |  STEP 3: Determine Quantity
      |  signal.Quantity ?? PositionSizer.CalculateQuantityAsync()
      |  --> FixedFractionPositionSizer:
      |      qty = (balance * MaxPositionSizePercent / 100) / price
      |  --> SKIP if quantity <= 0
      |
      |  STEP 4: Dry Run Check
      |  if (DryRunMode) --> log only, no real order
      |
      |  STEP 5: Place Order
      |  client.PlaceOrderAsync(order)
      |
      |  STEP 6: On Success
      |  --> PositionManager.RecordFill()     (update position book)
      |  --> AccountingService.RecordTradeAsync() (persist trade record)
      |
      |  STEP 7: On Failure
      |  --> Log error, do NOT record
      v
  [Order Executed or Skipped]
```

### Flow 2: Price Monitoring

```
PriceMonitorWorker (BackgroundService, 5s loop)
      |
      |  1. Wire OnPriceUpdate events -> PositionManager.UpdatePositionPrice()
      |  2. Every 5 seconds:
      |     - Scan all open positions
      |     - For each position without a price feed:
      |       auto-subscribe via IPriceMonitor
      |
      v
BinancePriceMonitor                    OandaPriceMonitor
  |                                      |
  |  WebSocket per symbol                |  Single SSE stream for all symbols
  |  Book ticker: best bid/ask           |  JSON lines: PRICE or HEARTBEAT
  |  Subscribe individually              |  Restart stream on symbol change
  |                                      |  Auto-reconnect with 5s backoff
  |                                      |
  +------------- OnPriceUpdate ----------+
                      |
                      v
          PositionManager.UpdatePositionPrice()
                      |
                      v
          Position.UpdateCurrentPrice(price)
            - Long:  PnL = (current - entry) * qty
            - Short: PnL = (entry - current) * qty
```

### Flow 3: Position Synchronization

```
PositionSyncWorker (BackgroundService, every 30s)
      |
      |  For each exchange (Binance, Oanda):
      |    1. client.GetOpenPositionsAsync()
      |    2. PositionManager.SyncPositions()
      |
      v
PositionManager.SyncPositions(exchange, exchangePositions)
      |
      |  Three checks:
      |  1. Quantity mismatch: local qty != exchange qty --> WARN
      |  2. Missing locally: exchange has position we don't --> WARN
      |  3. Extra locally: we have position exchange doesn't --> WARN
      |
      v
  [Warnings logged if divergence detected]
```

### Flow 4: Account Sync & Reconciliation

```
AccountSyncWorker (BackgroundService, every 60s)
      |
      |  For each exchange:
      |    1. client.GetAccountBalanceAsync()
      |    2. AccountingService.UpdateBalance()
      |    3. AccountingService.GetReconciliationReport()
      |
      v
AccountingService.GetReconciliationReport(exchange)
      |
      |  Local PnL:
      |    - Sum fees from trade history
      |    - Sum unrealized PnL from open positions
      |
      |  Exchange PnL:
      |    - From last balance update (unrealized PnL reported by exchange)
      |
      |  Diverged = |local.NetPnL - exchange.NetPnL| > ReconciliationThreshold
      |
      v
  if Diverged --> RECONCILIATION WARNING logged
  else        --> "Account sync OK" debug logged
```

### Flow 5: Dashboard Rendering

```
DashboardWorker (BackgroundService, every 1500ms)
      |
      |  Spectre.Console Live display (flicker-free)
      |
      v
DashboardRenderer.Render()
      |
      +-- BuildPositionsPanel()
      |     Open Positions table: Exchange, Symbol, Side, Qty,
      |     Entry, Current, Unrealized P&L (green/red)
      |
      +-- BuildAccountPanel()
      |     Per-exchange: Local P&L, Exchange P&L,
      |     Reconciliation status (OK/DIVERGED)
      |
      +-- BuildPricesPanel()
      |     Live prices: Exchange, Symbol, Bid, Ask,
      |     Spread, time since last update
      |
      +-- BuildStatusPanel()
            Uptime, UTC time, worker statuses
```

---

## Exchange Abstraction Layer

```
         IExchangeFactory
               |
               v
         ExchangeFactory
          (keyed DI lookup)
               |
     +---------+---------+
     |                   |
     v                   v
BinanceExchangeClient   OandaExchangeClient
     |                   |
     v                   v
IBinanceRestClient      OandaApiClient
(Binance.Net SDK)       (HttpClient wrapper)
     |                   |
     v                   v
BinanceOrderMapper      OandaOrderMapper
BinancePositionMapper   OandaPositionMapper
```

### Order Type Mapping

| Framework       | Binance (FuturesOrderType) | Oanda (string) |
|-----------------|---------------------------|-----------------|
| Market          | Market                    | "MARKET"        |
| Limit           | Limit                     | "LIMIT"         |
| StopMarket      | StopMarket                | "STOP"          |
| StopLimit       | Stop                      | "STOP"          |

### Order Status Mapping (Binance -> Local)

| Binance         | Local           |
|-----------------|-----------------|
| New             | Submitted       |
| PartiallyFilled | PartiallyFilled |
| Filled          | Filled          |
| Canceled        | Cancelled       |
| Rejected        | Rejected        |
| Expired         | Expired         |
| (unknown)       | Pending         |

---

## Data Model Relationships

```
TradeSignal (from Event Hub)
    |
    |  SignalParser.Parse()
    |  SignalDispatcher.DispatchAsync()
    |  OrderManager.ExecuteSignalAsync()
    |
    v
Order (built in OrderManager)
    |
    |  ExchangeClient.PlaceOrderAsync()
    |
    v
OrderResult (from exchange)
    |
    +-- Success --> TradeRecord (persisted to ITradeHistoryStore)
    |               Position (updated in PositionManager)
    |
    +-- Failure --> Error logged, nothing recorded

TradeSignal.SignalId --> Order.SignalId --> TradeRecord.SignalId
                        Order.OrderId  --> TradeRecord.OrderId
                        OrderResult.ExchangeOrderId --> TradeRecord.ExchangeOrderId
```

---

## Configuration (appsettings.json)

```json
{
  "Binance": {
    "ApiKey": "",
    "ApiSecret": "",
    "UseTestnet": true
  },
  "Oanda": {
    "ApiToken": "",
    "AccountId": "",
    "UsePractice": true
  },
  "EventHub": {
    "ConnectionString": "",
    "EventHubName": "",
    "ConsumerGroup": "$Default",
    "BlobStorageConnectionString": "",
    "BlobContainerName": "eventhub-checkpoints"
  },
  "Trading": {
    "DryRunMode": true,
    "MaxPositionSizePercent": 2.0,
    "MaxOpenPositions": 10,
    "ReconciliationThreshold": 1.0,
    "PositionSyncIntervalSeconds": 30,
    "AccountSyncIntervalSeconds": 60,
    "DashboardRefreshIntervalMs": 1500
  }
}
```

---

## Project Structure

```
testTradingBotFramework/
  Program.cs                          # Entry point
  appsettings.json                    # Configuration
  Configuration/
    BinanceSettings.cs                # Binance API credentials
    OandaSettings.cs                  # Oanda API credentials + computed URLs
    EventHubSettings.cs               # Azure Event Hub connection
    TradingSettings.cs                # Trading behavior (dry-run, sizing, limits)
  Extensions/
    ServiceCollectionExtensions.cs    # DI composition root
    LoggingExtensions.cs              # Serilog configuration
  Models/
    AccountBalance.cs                 # Exchange account balance snapshot
    Order.cs                          # Order with local + exchange IDs
    OrderResult.cs                    # Exchange response (Success/Failed factories)
    PnLSnapshot.cs                    # P&L summary with computed NetPnL
    Position.cs                       # Open position with UpdateCurrentPrice()
    TradeRecord.cs                    # Immutable trade fill record
    TradeSignal.cs                    # Inbound signal from Event Hub
    Enums/
      AssetClass.cs                   # CryptoFutures, CryptoSpot, Forex
      ExchangeName.cs                 # Binance, Oanda (keyed DI key)
      OrderSide.cs                    # Buy, Sell
      OrderStatus.cs                  # Pending -> Submitted -> Filled/Cancelled/...
      OrderType.cs                    # Market, Limit, StopMarket, StopLimit
      PositionSide.cs                 # Long, Short
      SignalAction.cs                 # Open (extensible)
  Exchanges/
    IExchangeClient.cs                # Core exchange abstraction (7 methods)
    IExchangeFactory.cs               # Factory for keyed DI resolution
    ExchangeFactory.cs                # Concrete factory
    Binance/
      BinanceExchangeClient.cs        # USD-M Futures via Binance.Net SDK
      BinanceOrderMapper.cs           # Framework <-> Binance enum mapping
      BinancePositionMapper.cs        # Binance position -> local Position
    Oanda/
      OandaApiClient.cs               # Low-level HTTP client for Oanda v3 API
      OandaExchangeClient.cs          # IExchangeClient for Oanda forex
      OandaOrderMapper.cs             # Framework <-> Oanda format mapping
      OandaPositionMapper.cs          # Oanda position -> local Position(s)
      OandaModels/
        OandaAccountResponse.cs       # GET /v3/accounts/{id} response
        OandaOrderRequest.cs          # POST /v3/accounts/{id}/orders body
        OandaOrderResponse.cs         # Order response (fill/cancel/create)
        OandaPricingResponse.cs       # Pricing REST + streaming models
  Services/
    EventProcessing/
      ISignalParser.cs                # JSON -> TradeSignal (null on error)
      SignalParser.cs                 # System.Text.Json implementation
      ISignalDispatcher.cs            # Routes signals to OrderManager
      SignalDispatcher.cs             # Catches exceptions for resilience
      EventHubListenerService.cs      # Azure Event Hub consumer (BackgroundService)
    OrderManagement/
      IOrderManager.cs                # Signal execution pipeline
      OrderManager.cs                 # Risk -> Size -> Place -> Record
    PositionManagement/
      IPositionManager.cs             # Position book interface
      PositionManager.cs              # ConcurrentDictionary-backed
      IPositionSizer.cs               # Money management abstraction
      FixedFractionPositionSizer.cs   # qty = (balance * %  / 100) / price
    Accounting/
      IAccountingService.cs           # Trade recording + P&L reconciliation
      AccountingService.cs            # Concrete implementation
      ITradeHistoryStore.cs           # Persistence abstraction
      InMemoryTradeHistoryStore.cs    # ConcurrentBag-backed (dev/testing)
    PriceMonitoring/
      IPriceMonitor.cs                # Real-time price feed abstraction
      PriceUpdateEventArgs.cs         # Bid/Ask/Mid event data
      Binance/
        BinancePriceMonitor.cs        # WebSocket book ticker
      Oanda/
        OandaPriceMonitor.cs          # SSE streaming with auto-reconnect
  Dashboard/
    DashboardRenderer.cs              # Spectre.Console 2x2 grid layout
    DashboardWorker.cs                # Live display refresh loop
  Workers/
    TradingBotWorker.cs               # Startup health checks + heartbeat
    PriceMonitorWorker.cs             # Auto-subscribe price feeds
    PositionSyncWorker.cs             # Position reconciliation (30s)
    AccountSyncWorker.cs              # Balance sync + P&L check (60s)

testTradingBotFramework.Tests/
  SignalParserTests.cs                # 5 tests: JSON parsing edge cases
  PositionManagerTests.cs             # 9 tests: position book operations
  AccountingServiceTests.cs           # 6 tests: P&L and reconciliation
  InMemoryTradeHistoryStoreTests.cs   # 5 tests: store CRUD and filtering
  OrderManagerTests.cs                # 7 tests: full pipeline scenarios
  FixedFractionPositionSizerTests.cs  # 3 tests: sizing formula and errors
  BinanceOrderMapperTests.cs          # 4 tests: all enum mappings
  OandaOrderMapperTests.cs            # 11 tests: both mapping directions
  ---
  Total: 61 tests (all xUnit with NSubstitute + FluentAssertions)
```
