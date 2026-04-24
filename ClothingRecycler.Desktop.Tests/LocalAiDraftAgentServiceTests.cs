using ClothingRecycler.Desktop.Models;
using ClothingRecycler.Desktop.Services;

namespace ClothingRecycler.Desktop.Tests;

public sealed class LocalAiDraftAgentServiceTests
{
    [Fact]
    public async Task SuggestInboundDraftAsync_ParsesFinalResponseAndResolvesEntities()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 1, Name = "夏装", UnitType = WeightUnit.Kilogram, BuyPrice = 3.5, SellPrice = 8.5, Stock = 20 },
                new CategoryModel { Id = 2, Name = "鞋子", UnitType = WeightUnit.Piece, BuyPrice = 10, SellPrice = 20, StockInPieces = 6 }
            ],
            customers:
            [
                new CustomerModel { Id = 11, Name = "王姐" },
                new CustomerModel { Id = 12, Name = "李老板" }
            ]);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            """
            {
              "intent": "fill_inbound_order",
              "confidence": 0.93,
              "customer_name": "王姐",
              "lines": [
                {
                  "category_name": "夏装",
                  "quantity": 12,
                  "unit_type": "Jin",
                  "unit_price": 3.8
                }
              ],
              "missing_fields": [],
              "warnings": [],
              "assistant_message": "Draft ready."
            }
            """);
        var service = new LocalAiDraftAgentService(context, [provider], new AppLogger());

        var suggestion = await service.SuggestInboundDraftAsync("王姐，夏装 12 斤，3.8 元一斤", provider.Kind);

        Assert.Equal(AiDraftOperation.Inbound, suggestion.Operation);
        Assert.Equal(11, suggestion.ExistingCustomerId);
        Assert.Single(suggestion.Lines);
        Assert.Equal(1, suggestion.Lines[0].ExistingCategoryId);
        Assert.Equal(WeightUnit.Jin, suggestion.Lines[0].InputUnitType);
        Assert.Equal(12d, suggestion.Lines[0].Quantity ?? 0d, 3);
        Assert.Equal(3.8d, suggestion.Lines[0].UnitPrice ?? 0d, 3);
        Assert.Empty(suggestion.Warnings);
    }

    [Fact]
    public async Task SuggestOutboundDraftAsync_ExecutesResolveEntitiesToolFlow()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 1, Name = "夏装", UnitType = WeightUnit.Kilogram, BuyPrice = 3.5, SellPrice = 8.5, Stock = 20 },
                new CategoryModel { Id = 2, Name = "鞋子", UnitType = WeightUnit.Piece, BuyPrice = 10, SellPrice = 20, StockInPieces = 6 }
            ],
            customers:
            [
                new CustomerModel { Id = 12, Name = "李老板" }
            ]);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.Ollama,
            """
            {
              "message_type": "tool_call",
              "tool_name": "resolve_entities",
              "arguments": {
                "customer_name": "李老板",
                "category_names": ["鞋子"]
              }
            }
            """,
            """
            {
              "intent": "fill_outbound_order",
              "confidence": 0.88,
              "customer_name": "李老板",
              "lines": [
                {
                  "category_name": "鞋子",
                  "quantity": 3,
                  "unit_type": "Piece",
                  "unit_price": 18
                }
              ],
              "missing_fields": [],
              "warnings": [],
              "assistant_message": "Outbound draft ready."
            }
            """);
        var service = new LocalAiDraftAgentService(context, [provider], new AppLogger());

        var suggestion = await service.SuggestOutboundDraftAsync("给李老板出库鞋子 3 件，单价 18", provider.Kind);

        Assert.Equal(AiDraftOperation.Outbound, suggestion.Operation);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Contains("Local tool result", provider.Requests[1].Messages[1].Content);
        Assert.Equal(12, suggestion.ExistingCustomerId);
        Assert.Equal(2, suggestion.Lines[0].ExistingCategoryId);
        Assert.Equal(WeightUnit.Piece, suggestion.Lines[0].InputUnitType);
        Assert.Equal(3d, suggestion.Lines[0].Quantity ?? 0d, 3);
    }

    [Fact]
    public async Task SuggestInboundDraftAsync_ParsesEscapedJsonStringResponse()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 1, Name = "夏装", UnitType = WeightUnit.Jin, BuyPrice = 3.8, SellPrice = 8.5, Stock = 20 }
            ],
            customers:
            [
                new CustomerModel { Id = 11, Name = "王姐" }
            ]);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            "\"{\\n  \\\"message_type\\\": \\\"final\\\",\\n  \\\"payload\\\": {\\n    \\\"intent\\\": \\\"fill_inbound_order\\\",\\n    \\\"confidence\\\": 0.9,\\n    \\\"customer_name\\\": \\\"王姐\\\",\\n    \\\"lines\\\": [{\\n      \\\"category_name\\\": \\\"夏装\\\",\\n      \\\"quantity\\\": 12,\\n      \\\"unit_type\\\": \\\"Jin\\\",\\n      \\\"unit_price\\\": 3.8\\n    }],\\n    \\\"missing_fields\\\": [],\\n    \\\"warnings\\\": [],\\n    \\\"assistant_message\\\": \\\"Draft ready.\\\"\\n  }\\n}\"");
        var service = new LocalAiDraftAgentService(context, [provider], new AppLogger());

        var suggestion = await service.SuggestInboundDraftAsync("王姐入库夏装 12 斤，3 块 8", provider.Kind);

        Assert.Equal("王姐", suggestion.CustomerName);
        Assert.Single(suggestion.Lines);
        Assert.Equal("夏装", suggestion.Lines[0].CategoryName);
        Assert.Equal(12d, suggestion.Lines[0].Quantity ?? 0d, 3);
        Assert.Equal(3.8d, suggestion.Lines[0].UnitPrice ?? 0d, 3);
    }

    [Fact]
    public async Task SuggestInboundDraftAsync_FallsBackWhenModelDraftIsObviouslyBroken()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 1, Name = "夏装", UnitType = WeightUnit.Jin, BuyPrice = 3.8, SellPrice = 8.5, Stock = 20 }
            ],
            customers:
            [
                new CustomerModel { Id = 11, Name = "王姐" }
            ]);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            """
            {
              "message_type": "final",
              "payload": {
                "intent": "fill_inbound_order",
                "confidence": 0.0,
                "customer_name": "",
                "lines": [
                  {
                    "category_name": "name",
                    "quantity": 12,
                    "unit_type": "Jin",
                    "unit_price": 3.8
                  }
                ],
                "missing_fields": [],
                "warnings": [],
                "assistant_message": "Bad draft."
              }
            }
            """);
        var service = new LocalAiDraftAgentService(context, [provider], new AppLogger());

        var suggestion = await service.SuggestInboundDraftAsync("王姐入库夏装 12 斤，3 块 8", provider.Kind);

        Assert.Equal("王姐", suggestion.CustomerName);
        Assert.Single(suggestion.Lines);
        Assert.Equal("夏装", suggestion.Lines[0].CategoryName);
        Assert.Equal(12d, suggestion.Lines[0].Quantity ?? 0d, 3);
        Assert.Equal(3.8d, suggestion.Lines[0].UnitPrice ?? 0d, 3);
        Assert.Contains(suggestion.Warnings, warning => warning.Contains("best-effort local draft", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SuggestInboundDraftAsync_FallsBackWhenModelReturnsTruncatedJson()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 1, Name = "\u590f\u88c5", UnitType = WeightUnit.Jin, BuyPrice = 3.8, SellPrice = 8.5, Stock = 20 }
            ],
            customers:
            [
                new CustomerModel { Id = 11, Name = "\u738b\u59d0" }
            ]);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            """
            {
              "orderDraft": {
                "customerName": "John Doe",
                "items": [
                  {
                    "itemCode": "ABC-001",
                    "quantity": 2,
                    "unitPrice": 19.99,
                    "totalValue":
            """);
        var service = new LocalAiDraftAgentService(context, [provider], new AppLogger());

        var suggestion = await service.SuggestInboundDraftAsync("\u738b\u59d0\u5165\u5e93\u590f\u88c5 12 \u65a4\uff0c3 \u5757 8", provider.Kind);

        Assert.Equal("\u738b\u59d0", suggestion.CustomerName);
        Assert.Single(suggestion.Lines);
        Assert.Equal("\u590f\u88c5", suggestion.Lines[0].CategoryName);
        Assert.Equal(12d, suggestion.Lines[0].Quantity ?? 0d, 3);
        Assert.Equal(3.8d, suggestion.Lines[0].UnitPrice ?? 0d, 3);
        Assert.Contains(suggestion.Warnings, warning => warning.Contains("best-effort local draft", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SuggestInboundDraftAsync_FallbackExtractsNewCustomerNameFromInput()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 1, Name = "\u590f\u88c5", UnitType = WeightUnit.Jin, BuyPrice = 3.8, SellPrice = 8.5, Stock = 20 }
            ],
            customers:
            [
                new CustomerModel { Id = 99, Name = "\u674E\u8001\u677F" }
            ]);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            """
            {
              "message_type": "final",
              "payload": {
                "intent": "fill_inbound_order",
                "confidence": 0.0,
                "customer_name": "",
                "lines": [],
                "missing_fields": [],
                "warnings": [],
                "assistant_message": "Bad draft."
              }
            }
            """);
        var service = new LocalAiDraftAgentService(context, [provider], new AppLogger());

        var suggestion = await service.SuggestInboundDraftAsync("\u738b\u59d0\u5165\u5e93\u590f\u88c5 12 \u65a4\uff0c3 \u5757 8", provider.Kind);

        Assert.Equal("\u738b\u59d0", suggestion.CustomerName);
        Assert.Null(suggestion.ExistingCustomerId);
        Assert.Single(suggestion.Lines);
        Assert.Equal("\u590f\u88c5", suggestion.Lines[0].CategoryName);
        Assert.Equal(12d, suggestion.Lines[0].Quantity ?? 0d, 3);
        Assert.Equal(3.8d, suggestion.Lines[0].UnitPrice ?? 0d, 3);
    }

    [Fact]
    public async Task SuggestInboundDraftAsync_FallbackParsesCurrencySymbolPricePerJin()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 1, Name = "\u590f\u88c5", UnitType = WeightUnit.Jin, BuyPrice = 0.35, SellPrice = 8.5, Stock = 20 }
            ],
            customers:
            [
                new CustomerModel { Id = 11, Name = "\u738b\u59d0" }
            ]);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            """
            {
              "message_type": "final",
              "payload": {
                "intent": "fill_inbound_order",
                "confidence": 0.0,
                "customer_name": "",
                "lines": [],
                "missing_fields": [],
                "warnings": [],
                "assistant_message": "Bad draft."
              }
            }
            """);
        var service = new LocalAiDraftAgentService(context, [provider], new AppLogger());

        var suggestion = await service.SuggestInboundDraftAsync("\u738b\u59d0\u5165\u5e93\u590f\u88c5 12 \u65a4\uff0c0.28\uffe5\u6bcf\u65a4", provider.Kind);

        Assert.Equal("\u738b\u59d0", suggestion.CustomerName);
        Assert.Single(suggestion.Lines);
        Assert.Equal("\u590f\u88c5", suggestion.Lines[0].CategoryName);
        Assert.Equal(12d, suggestion.Lines[0].Quantity ?? 0d, 3);
        Assert.Equal(0.28d, suggestion.Lines[0].UnitPrice ?? 0d, 3);
        Assert.DoesNotContain("unit_price", suggestion.MissingFields);
    }

    [Fact]
    public async Task TranscribeAudioAsync_SendsAudioPathAndCleansTranscript()
    {
        var audioPath = Path.Combine(Path.GetTempPath(), $"cr-voice-{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(audioPath, [0, 0, 0, 0]);
        try
        {
            var context = new FakeBusinessContextSource(categories: [], customers: []);
            var provider = new StubLocalAiProvider(
                AiLocalProviderKind.HuggingFaceUltravoxPython,
                """
                {"text":"刘建斌入库羽绒服33公斤，每公斤2元"}
                """);
            var service = new LocalAiDraftAgentService(context, [provider], new AppLogger());

            var transcript = await service.TranscribeAudioAsync(audioPath, provider.Kind);

            Assert.Equal("刘建斌入库羽绒服33公斤，每公斤2元", transcript);
            Assert.Single(provider.Requests);
            Assert.Equal(audioPath, provider.Requests[0].AudioPath);
            Assert.Contains("转写", provider.Requests[0].Messages[1].Content);
        }
        finally
        {
            File.Delete(audioPath);
        }
    }

    private sealed class FakeBusinessContextSource : IAiBusinessContextSource
    {
        private readonly IReadOnlyList<CategoryModel> _categories;
        private readonly IReadOnlyList<CustomerModel> _customers;

        public FakeBusinessContextSource(IReadOnlyList<CategoryModel> categories, IReadOnlyList<CustomerModel> customers)
        {
            _categories = categories;
            _customers = customers;
        }

        public Task<IReadOnlyList<CategoryModel>> GetActiveCategoriesAsync()
        {
            return Task.FromResult(_categories);
        }

        public Task<IReadOnlyList<CustomerModel>> GetCustomersAsync()
        {
            return Task.FromResult(_customers);
        }
    }

    private sealed class StubLocalAiProvider : ILocalAiProvider
    {
        private readonly Queue<string> _responses;

        public StubLocalAiProvider(AiLocalProviderKind kind, params string[] responses)
        {
            Kind = kind;
            _responses = new Queue<string>(responses);
        }

        public AiLocalProviderKind Kind { get; }

        public List<AiCompletionRequest> Requests { get; } = [];

        public Task<AiProviderProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiProviderProbeResult
            {
                Kind = Kind,
                IsAvailable = true,
                Summary = "Stub provider is ready.",
                Detail = string.Empty
            });
        }

        public Task<string> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
