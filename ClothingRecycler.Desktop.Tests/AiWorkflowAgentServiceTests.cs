using ClothingRecycler.Desktop.Models;
using ClothingRecycler.Desktop.Services;

namespace ClothingRecycler.Desktop.Tests;

public sealed class AiWorkflowAgentServiceTests
{
    [Fact]
    public async Task RunAsync_CompleteInboundDraft_AutoRoutesToInboundConfirmation()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 1, Name = "夏装", UnitType = WeightUnit.Jin, BuyPrice = 4.2, SellPrice = 8.5, Stock = 20 }
            ],
            customers:
            [
                new CustomerModel { Id = 11, Name = "王姐" }
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

        var handoffService = new AiDraftHandoffService();
        var navigationService = new ShellNavigationService();
        string? navigatedTag = null;
        navigationService.RegisterNavigator(tag =>
        {
            navigatedTag = tag;
            return true;
        });

        var draftAgentService = new LocalAiDraftAgentService(context, [provider], new AppLogger());
        var toolService = new AiWorkflowToolService(handoffService, navigationService);
        var normalizationToolService = new AiDraftNormalizationToolService(context);
        var agentService = new AiWorkflowAgentService(draftAgentService, normalizationToolService, toolService);

        var result = await agentService.RunAsync(
            AiDraftOperation.Inbound,
            "王姐入库夏装 12 斤，3 块 8",
            provider.Kind);

        Assert.True(result.Success);
        Assert.Equal(AiWorkflowToolNames.PreviewInboundConfirmation, result.ToolName);
        Assert.Equal("inbound", navigatedTag);
        Assert.NotNull(result.Suggestion);
        Assert.Equal(3.8d, result.Suggestion!.Lines.Single().UnitPrice ?? 0d, 3);
        Assert.Equal(5, result.TraceSteps.Count);
        Assert.Contains(result.TraceSteps, step =>
            step.Title.Contains("\u5de5\u5177\u9009\u62e9", StringComparison.Ordinal)
            && step.Detail.Contains(AiWorkflowToolNames.PreviewInboundConfirmation, StringComparison.Ordinal));

        var pendingRequest = handoffService.Consume(AiDraftOperation.Inbound);
        Assert.NotNull(pendingRequest);
        Assert.Equal(AiDraftLaunchMode.ConfirmationPreview, pendingRequest!.LaunchMode);
        Assert.Equal("王姐", pendingRequest.Suggestion.CustomerName);
    }

    [Fact]
    public void Execute_CreateOutboundOrder_StoresConfirmationPreviewRequest()
    {
        var handoffService = new AiDraftHandoffService();
        var navigationService = new ShellNavigationService();
        string? navigatedTag = null;
        navigationService.RegisterNavigator(tag =>
        {
            navigatedTag = tag;
            return true;
        });

        var toolService = new AiWorkflowToolService(handoffService, navigationService);
        var suggestion = new AiOrderDraftSuggestion
        {
            Operation = AiDraftOperation.Outbound,
            ProviderKind = AiLocalProviderKind.Ollama,
            Confidence = 0.82,
            CustomerName = "李老板",
            ExistingCustomerId = 12,
            Lines =
            [
                new AiOrderDraftLineSuggestion
                {
                    CategoryName = "鞋子",
                    ExistingCategoryId = 2,
                    Quantity = 3,
                    InputUnitType = WeightUnit.Piece,
                    UnitPrice = 18
                }
            ],
            AssistantMessage = "Outbound draft ready."
        };

        var result = toolService.Execute(AiWorkflowToolNames.CreateOutboundOrder, suggestion);

        Assert.True(result.Success);
        Assert.Equal("outbound", navigatedTag);
        Assert.Equal(AiDraftLaunchMode.ConfirmationPreview, result.RecommendedLaunchMode);

        var pendingRequest = handoffService.Consume(AiDraftOperation.Outbound);
        Assert.NotNull(pendingRequest);
        Assert.Equal(AiDraftLaunchMode.ConfirmationPreview, pendingRequest!.LaunchMode);
        Assert.Equal("李老板", pendingRequest.Suggestion.CustomerName);
    }

    [Fact]
    public void GetRecommendedToolName_IncompleteInboundDraft_StaysAsDraftOnly()
    {
        var toolService = new AiWorkflowToolService(new AiDraftHandoffService(), new ShellNavigationService());
        var suggestion = new AiOrderDraftSuggestion
        {
            Operation = AiDraftOperation.Inbound,
            ProviderKind = AiLocalProviderKind.HuggingFaceUltravoxPython,
            Confidence = 0.4,
            CustomerName = string.Empty,
            Lines =
            [
                new AiOrderDraftLineSuggestion
                {
                    CategoryName = "夏装",
                    ExistingCategoryId = 1,
                    Quantity = 12,
                    InputUnitType = WeightUnit.Jin,
                    UnitPrice = null
                }
            ],
            MissingFields = ["customer_name", "unit_price"]
        };

        var toolName = toolService.GetRecommendedToolName(suggestion);
        var result = toolService.Execute(toolName, suggestion);

        Assert.Equal(AiWorkflowToolNames.CreateInboundDraft, toolName);
        Assert.True(result.Success);
        Assert.False(result.CanAutoExecute);
        Assert.Null(result.RecommendedLaunchMode);
    }

    [Fact]
    public async Task RunAsync_FallbackWithCurrencySymbolPrice_StillRoutesToInboundConfirmation()
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

        var handoffService = new AiDraftHandoffService();
        var navigationService = new ShellNavigationService();
        string? navigatedTag = null;
        navigationService.RegisterNavigator(tag =>
        {
            navigatedTag = tag;
            return true;
        });

        var draftAgentService = new LocalAiDraftAgentService(context, [provider], new AppLogger());
        var toolService = new AiWorkflowToolService(handoffService, navigationService);
        var normalizationToolService = new AiDraftNormalizationToolService(context);
        var agentService = new AiWorkflowAgentService(draftAgentService, normalizationToolService, toolService);

        var result = await agentService.RunAsync(
            AiDraftOperation.Inbound,
            "\u738b\u59d0\u5165\u5e93\u590f\u88c5 12 \u65a4\uff0c0.28\uffe5\u6bcf\u65a4",
            provider.Kind);

        Assert.True(result.Success);
        Assert.Equal(AiWorkflowToolNames.PreviewInboundConfirmation, result.ToolName);
        Assert.Equal("inbound", navigatedTag);
        Assert.NotNull(result.Suggestion);
        Assert.Equal(0.28d, result.Suggestion!.Lines.Single().UnitPrice ?? 0d, 3);

        var pendingRequest = handoffService.Consume(AiDraftOperation.Inbound);
        Assert.NotNull(pendingRequest);
        Assert.Equal(AiDraftLaunchMode.ConfirmationPreview, pendingRequest!.LaunchMode);
    }

    [Fact]
    public async Task RunAsync_ModelDraftMissingUnitPrice_NormalizesFromPromptAndRoutesToConfirmation()
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
              "intent": "fill_inbound_order",
              "confidence": 0.74,
              "customer_name": "王姐",
              "lines": [
                {
                  "category_name": "夏装",
                  "quantity": 12,
                  "unit_type": "Jin"
                }
              ],
              "missing_fields": ["unit_price"],
              "warnings": ["Need price confirmation."],
              "assistant_message": "Draft missing price."
            }
            """);

        var handoffService = new AiDraftHandoffService();
        var navigationService = new ShellNavigationService();
        string? navigatedTag = null;
        navigationService.RegisterNavigator(tag =>
        {
            navigatedTag = tag;
            return true;
        });

        var draftAgentService = new LocalAiDraftAgentService(context, [provider], new AppLogger());
        var normalizationToolService = new AiDraftNormalizationToolService(context);
        var toolService = new AiWorkflowToolService(handoffService, navigationService);
        var agentService = new AiWorkflowAgentService(draftAgentService, normalizationToolService, toolService);

        var result = await agentService.RunAsync(
            AiDraftOperation.Inbound,
            "\u738b\u59d0\u5165\u5e93\u590f\u88c5 12 \u65a4\uff0c0.28\uffe5\u6bcf\u65a4",
            provider.Kind);

        Assert.True(result.Success);
        Assert.Equal(AiWorkflowToolNames.PreviewInboundConfirmation, result.ToolName);
        Assert.Equal("inbound", navigatedTag);
        Assert.NotNull(result.Suggestion);
        Assert.Equal(0.28d, result.Suggestion!.Lines.Single().UnitPrice ?? 0d, 3);
        Assert.DoesNotContain("unit_price", result.Suggestion.MissingFields);
        Assert.Equal(5, result.TraceSteps.Count);
        Assert.Contains(result.TraceSteps, step =>
            step.Title.Contains("\u89c4\u8303\u5316\u5de5\u5177", StringComparison.Ordinal)
            && step.Detail.Contains("price=0.28", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_NewCustomerBeforeComma_NormalizesAndRoutesToInboundConfirmation()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 7, Name = "\u7fbd\u7ed2\u670d", UnitType = WeightUnit.Kilogram, BuyPrice = 1.5, SellPrice = 8.5, Stock = 20 }
            ],
            customers: []);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            """
            {
              "intent": "fill_inbound_order",
              "confidence": 0.74,
              "customer_name": "",
              "lines": [
                {
                  "category_name": "羽绒服",
                  "quantity": 33,
                  "unit_type": "Kilogram",
                  "unit_price": 2
                }
              ],
              "missing_fields": ["customer_name"],
              "warnings": ["Need customer confirmation."],
              "assistant_message": "Draft missing customer."
            }
            """);

        var handoffService = new AiDraftHandoffService();
        var navigationService = new ShellNavigationService();
        string? navigatedTag = null;
        navigationService.RegisterNavigator(tag =>
        {
            navigatedTag = tag;
            return true;
        });

        var draftAgentService = new LocalAiDraftAgentService(context, [provider], new AppLogger());
        var normalizationToolService = new AiDraftNormalizationToolService(context);
        var toolService = new AiWorkflowToolService(handoffService, navigationService);
        var agentService = new AiWorkflowAgentService(draftAgentService, normalizationToolService, toolService);

        var result = await agentService.RunAsync(
            AiDraftOperation.Inbound,
            "\u5218\u5efa\u658c\uff0c\u7fbd\u7ed2\u670d\u5165\u5e9333\u516c\u65a4\uff0c\u6bcf\u516c\u65a42\uffe5",
            provider.Kind);

        Assert.True(result.Success);
        Assert.Equal(AiWorkflowToolNames.PreviewInboundConfirmation, result.ToolName);
        Assert.Equal("inbound", navigatedTag);
        Assert.NotNull(result.Suggestion);
        Assert.Equal("\u5218\u5efa\u658c", result.Suggestion!.CustomerName);
        Assert.Equal(7, result.Suggestion.Lines.Single().ExistingCategoryId);
        Assert.Equal(33d, result.Suggestion.Lines.Single().Quantity ?? 0d, 3);
        Assert.Equal(2d, result.Suggestion.Lines.Single().UnitPrice ?? 0d, 3);
        Assert.DoesNotContain("customer_name", result.Suggestion.MissingFields);
    }

    [Fact]
    public async Task RunAsync_PriceWithoutQuantity_AsksForQuantityInsteadOfOpeningConfirmation()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 7, Name = "\u7fbd\u7ed2\u670d", UnitType = WeightUnit.Kilogram, BuyPrice = 1.5, SellPrice = 8.5, Stock = 20 }
            ],
            customers: []);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            """
            {
              "intent": "fill_inbound_order",
              "confidence": 0.74,
              "customer_name": "",
              "lines": [
                {
                  "category_name": "\u7fbd\u7ed2\u670d",
                  "quantity": 2,
                  "unit_type": "Kilogram",
                  "unit_price": 2
                }
              ],
              "missing_fields": [],
              "warnings": [],
              "assistant_message": "Draft ready."
            }
            """);

        var handoffService = new AiDraftHandoffService();
        var navigationService = new ShellNavigationService();
        string? navigatedTag = null;
        navigationService.RegisterNavigator(tag =>
        {
            navigatedTag = tag;
            return true;
        });

        var draftAgentService = new LocalAiDraftAgentService(context, [provider], new AppLogger());
        var normalizationToolService = new AiDraftNormalizationToolService(context);
        var toolService = new AiWorkflowToolService(handoffService, navigationService);
        var agentService = new AiWorkflowAgentService(draftAgentService, normalizationToolService, toolService);

        var result = await agentService.RunAsync(
            AiDraftOperation.Inbound,
            "\u5218\u5efa\u658c\uff0c\u7fbd\u7ed2\u670d\u5165\u5e93\uff0c2\uffe5\u6bcf\u516c\u65a4",
            provider.Kind);

        Assert.True(result.Success);
        Assert.Equal(AiWorkflowToolNames.CreateInboundDraft, result.ToolName);
        Assert.Null(navigatedTag);
        Assert.NotNull(result.Suggestion);
        Assert.Equal("\u5218\u5efa\u658c", result.Suggestion!.CustomerName);
        Assert.Equal(7, result.Suggestion.Lines.Single().ExistingCategoryId);
        Assert.Null(result.Suggestion.Lines.Single().Quantity);
        Assert.Equal(2d, result.Suggestion.Lines.Single().UnitPrice ?? 0d, 3);
        Assert.Contains("quantity", result.Suggestion.MissingFields);
        Assert.Null(handoffService.Consume(AiDraftOperation.Inbound));
    }

    [Fact]
    public async Task RunAsync_SupplementalQuantity_MergesWithPreviousDraftAndRoutesToConfirmation()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 7, Name = "\u7fbd\u7ed2\u670d", UnitType = WeightUnit.Kilogram, BuyPrice = 1.5, SellPrice = 8.5, Stock = 20 }
            ],
            customers: []);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            """
            {
              "intent": "fill_inbound_order",
              "confidence": 0.5,
              "customer_name": "",
              "lines": [
                {
                  "category_name": "",
                  "quantity": 33,
                  "unit_type": "Kilogram"
                }
              ],
              "missing_fields": ["customer_name", "category_name", "unit_price"],
              "warnings": [],
              "assistant_message": "Supplement received."
            }
            """);
        var previousDraft = new AiOrderDraftSuggestion
        {
            Operation = AiDraftOperation.Inbound,
            ProviderKind = provider.Kind,
            Confidence = 0.74,
            CustomerName = "\u5218\u5efa\u658c",
            Lines =
            [
                new AiOrderDraftLineSuggestion
                {
                    CategoryName = "\u7fbd\u7ed2\u670d",
                    ExistingCategoryId = 7,
                    Quantity = null,
                    InputUnitType = WeightUnit.Kilogram,
                    UnitPrice = 2
                }
            ],
            MissingFields = ["quantity"]
        };

        var handoffService = new AiDraftHandoffService();
        var navigationService = new ShellNavigationService();
        string? navigatedTag = null;
        navigationService.RegisterNavigator(tag =>
        {
            navigatedTag = tag;
            return true;
        });

        var draftAgentService = new LocalAiDraftAgentService(context, [provider], new AppLogger());
        var normalizationToolService = new AiDraftNormalizationToolService(context);
        var toolService = new AiWorkflowToolService(handoffService, navigationService);
        var agentService = new AiWorkflowAgentService(draftAgentService, normalizationToolService, toolService);

        var result = await agentService.RunAsync(
            AiDraftOperation.Inbound,
            "33\u516c\u65a4",
            provider.Kind,
            previousDraft);

        Assert.True(result.Success);
        Assert.Equal(AiWorkflowToolNames.PreviewInboundConfirmation, result.ToolName);
        Assert.Equal("inbound", navigatedTag);
        Assert.NotNull(result.Suggestion);
        Assert.Equal("\u5218\u5efa\u658c", result.Suggestion!.CustomerName);
        Assert.Equal(7, result.Suggestion.Lines.Single().ExistingCategoryId);
        Assert.Equal(33d, result.Suggestion.Lines.Single().Quantity ?? 0d, 3);
        Assert.Equal(2d, result.Suggestion.Lines.Single().UnitPrice ?? 0d, 3);
        Assert.Empty(result.Suggestion.MissingFields);
        Assert.Contains(result.TraceSteps, step =>
            step.Title.Contains("\u4f1a\u8bdd\u8bb0\u5fc6\u5408\u5e76", StringComparison.Ordinal));

        var pendingRequest = handoffService.Consume(AiDraftOperation.Inbound);
        Assert.NotNull(pendingRequest);
        Assert.Equal(AiDraftLaunchMode.ConfirmationPreview, pendingRequest!.LaunchMode);
    }

    [Fact]
    public async Task RunAutoAsync_ExplicitOutbound_RoutesToOutboundOrder()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 2, Name = "\u978b\u5b50", UnitType = WeightUnit.Piece, BuyPrice = 3, SellPrice = 18, Stock = 10 }
            ],
            customers:
            [
                new CustomerModel { Id = 12, Name = "\u674e\u8001\u677f" }
            ]);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            """
            {
              "intent": "fill_outbound_order",
              "confidence": 0.91,
              "customer_name": "\u674e\u8001\u677f",
              "lines": [
                {
                  "category_name": "\u978b\u5b50",
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

        var handoffService = new AiDraftHandoffService();
        var navigationService = new ShellNavigationService();
        string? navigatedTag = null;
        navigationService.RegisterNavigator(tag =>
        {
            navigatedTag = tag;
            return true;
        });

        var draftAgentService = new LocalAiDraftAgentService(context, [provider], new AppLogger());
        var normalizationToolService = new AiDraftNormalizationToolService(context);
        var toolService = new AiWorkflowToolService(handoffService, navigationService);
        var agentService = new AiWorkflowAgentService(draftAgentService, normalizationToolService, toolService);

        var result = await agentService.RunAutoAsync(
            "\u7ed9\u674e\u8001\u677f\u51fa\u5e93\u978b\u5b503\u4ef6\uff0c\u5355\u4ef718",
            provider.Kind);

        Assert.True(result.Success);
        Assert.Equal(AiWorkflowToolNames.CreateOutboundOrder, result.ToolName);
        Assert.Equal("outbound", navigatedTag);
        Assert.NotNull(result.Suggestion);
        Assert.Equal(AiDraftOperation.Outbound, result.Suggestion!.Operation);
        Assert.Equal(3d, result.Suggestion.Lines.Single().Quantity ?? 0d, 3);
        Assert.Contains(result.TraceSteps, step =>
            step.Title.Contains("\u610f\u56fe\u8bc6\u522b", StringComparison.Ordinal)
            && step.Detail.Contains("resolved_operation=Outbound", StringComparison.Ordinal));

        var pendingRequest = handoffService.Consume(AiDraftOperation.Outbound);
        Assert.NotNull(pendingRequest);
        Assert.Equal(AiDraftLaunchMode.ConfirmationPreview, pendingRequest!.LaunchMode);
    }

    [Fact]
    public async Task RunAutoAsync_AmbiguousWithoutMemory_AsksForOperation()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 7, Name = "\u7fbd\u7ed2\u670d", UnitType = WeightUnit.Kilogram, BuyPrice = 1.5, SellPrice = 8.5, Stock = 20 }
            ],
            customers: []);
        var provider = new StubLocalAiProvider(AiLocalProviderKind.HuggingFaceUltravoxPython);
        var handoffService = new AiDraftHandoffService();
        var navigationService = new ShellNavigationService();
        string? navigatedTag = null;
        navigationService.RegisterNavigator(tag =>
        {
            navigatedTag = tag;
            return true;
        });

        var draftAgentService = new LocalAiDraftAgentService(context, [provider], new AppLogger());
        var normalizationToolService = new AiDraftNormalizationToolService(context);
        var toolService = new AiWorkflowToolService(handoffService, navigationService);
        var agentService = new AiWorkflowAgentService(draftAgentService, normalizationToolService, toolService);

        var result = await agentService.RunAutoAsync(
            "\u5218\u5efa\u658c\uff0c\u7fbd\u7ed2\u670d33\u516c\u65a4\uff0c2\u5143\u6bcf\u516c\u65a4",
            provider.Kind);

        Assert.False(result.Success);
        Assert.Equal(AiWorkflowToolNames.AskForOperation, result.ToolName);
        Assert.Null(result.Suggestion);
        Assert.Null(navigatedTag);
        Assert.Contains("\u5165\u5e93\u8fd8\u662f\u51fa\u5e93", result.Summary, StringComparison.Ordinal);
        Assert.Null(handoffService.Consume(AiDraftOperation.Inbound));
        Assert.Null(handoffService.Consume(AiDraftOperation.Outbound));
    }

    [Fact]
    public async Task RunAutoAsync_SupplementWithoutOperation_UsesMemoryOperation()
    {
        var context = new FakeBusinessContextSource(
            categories:
            [
                new CategoryModel { Id = 7, Name = "\u7fbd\u7ed2\u670d", UnitType = WeightUnit.Kilogram, BuyPrice = 1.5, SellPrice = 8.5, Stock = 20 }
            ],
            customers: []);
        var provider = new StubLocalAiProvider(
            AiLocalProviderKind.HuggingFaceUltravoxPython,
            """
            {
              "intent": "fill_inbound_order",
              "confidence": 0.5,
              "customer_name": "",
              "lines": [
                {
                  "category_name": "",
                  "quantity": 33,
                  "unit_type": "Kilogram"
                }
              ],
              "missing_fields": ["customer_name", "category_name", "unit_price"],
              "warnings": [],
              "assistant_message": "Supplement received."
            }
            """);
        var previousDraft = new AiOrderDraftSuggestion
        {
            Operation = AiDraftOperation.Inbound,
            ProviderKind = provider.Kind,
            Confidence = 0.74,
            CustomerName = "\u5218\u5efa\u658c",
            Lines =
            [
                new AiOrderDraftLineSuggestion
                {
                    CategoryName = "\u7fbd\u7ed2\u670d",
                    ExistingCategoryId = 7,
                    Quantity = null,
                    InputUnitType = WeightUnit.Kilogram,
                    UnitPrice = 2
                }
            ],
            MissingFields = ["quantity"]
        };

        var handoffService = new AiDraftHandoffService();
        var navigationService = new ShellNavigationService();
        string? navigatedTag = null;
        navigationService.RegisterNavigator(tag =>
        {
            navigatedTag = tag;
            return true;
        });

        var draftAgentService = new LocalAiDraftAgentService(context, [provider], new AppLogger());
        var normalizationToolService = new AiDraftNormalizationToolService(context);
        var toolService = new AiWorkflowToolService(handoffService, navigationService);
        var agentService = new AiWorkflowAgentService(draftAgentService, normalizationToolService, toolService);

        var result = await agentService.RunAutoAsync(
            "33\u516c\u65a4",
            provider.Kind,
            previousDraft);

        Assert.True(result.Success);
        Assert.Equal(AiWorkflowToolNames.PreviewInboundConfirmation, result.ToolName);
        Assert.Equal("inbound", navigatedTag);
        Assert.NotNull(result.Suggestion);
        Assert.Equal(AiDraftOperation.Inbound, result.Suggestion!.Operation);
        Assert.Equal("\u5218\u5efa\u658c", result.Suggestion.CustomerName);
        Assert.Equal(33d, result.Suggestion.Lines.Single().Quantity ?? 0d, 3);
        Assert.Equal(2d, result.Suggestion.Lines.Single().UnitPrice ?? 0d, 3);
        Assert.Contains(result.TraceSteps, step =>
            step.Title.Contains("\u610f\u56fe\u8bc6\u522b", StringComparison.Ordinal)
            && step.Detail.Contains("memory_operation=Inbound", StringComparison.Ordinal)
            && step.Detail.Contains("resolved_operation=Inbound", StringComparison.Ordinal));
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
            return Task.FromResult(_responses.Dequeue());
        }

        public Task WarmAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
