using System.Text.Json;
using AzureApimMcp.Functions.Functions;
using AzureApimMcp.Functions.Services;
using AzureApimMcp.Functions.Tests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AzureApimMcp.Functions.Tests.Functions;

public class ChatFunctionTests
{
    private readonly Mock<IChatClient> _chatClientMock = new();
    private readonly Mock<IApimService> _apimServiceMock = new();
    private readonly ILogger<ChatFunction> _logger = NullLogger<ChatFunction>.Instance;
    private readonly ChatFunction _sut;

    public ChatFunctionTests()
    {
        _sut = new ChatFunction(_chatClientMock.Object, _apimServiceMock.Object, _logger);
    }

    [Fact]
    public async Task Chat_ReturnsBadRequest_WhenMessageIsNull()
    {
        var request = TestHttpRequestHelper.CreatePost(new { sessionId = "test" });

        var result = await _sut.Chat(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("Message is required", json);
    }

    [Fact]
    public async Task Chat_ReturnsBadRequest_WhenMessageIsEmpty()
    {
        var request = TestHttpRequestHelper.CreatePost(new { message = "", sessionId = "test" });

        var result = await _sut.Chat(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Chat_ReturnsBadRequest_WhenMessageIsWhitespace()
    {
        var request = TestHttpRequestHelper.CreatePost(new { message = "   ", sessionId = "test" });

        var result = await _sut.Chat(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Chat_ReturnsOk_WithAIResponse()
    {
        var sessionId = $"test-{Guid.NewGuid()}"; // unique to avoid cross-test pollution
        var responseMessage = new ChatMessage(ChatRole.Assistant, "Here are the APIs I found.");
        var chatResponse = new ChatResponse([responseMessage])
        {
            ModelId = "gpt-4o-mini"
        };

        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        var request = TestHttpRequestHelper.CreatePost(new { message = "list all APIs", sessionId });

        var result = await _sut.Chat(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("Here are the APIs I found", json);
        Assert.Contains(sessionId, json);
    }

    [Fact]
    public async Task Chat_UsesDefaultSessionId_WhenNotProvided()
    {
        var responseMessage = new ChatMessage(ChatRole.Assistant, "Response");
        var chatResponse = new ChatResponse([responseMessage]) { ModelId = "test" };

        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatResponse);

        var request = TestHttpRequestHelper.CreatePost(new { message = "hello" });

        var result = await _sut.Chat(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("default", json);
    }

    [Fact]
    public async Task Chat_Returns500_WhenChatClientThrows()
    {
        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("OpenAI error"));

        var sessionId = $"error-{Guid.NewGuid()}";
        var request = TestHttpRequestHelper.CreatePost(new { message = "test", sessionId });

        var result = await _sut.Chat(request);

        var errorResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, errorResult.StatusCode);
        var json = JsonSerializer.Serialize(errorResult.Value);
        Assert.Contains("OpenAI error", json);
    }

    [Fact]
    public async Task Chat_PassesToolsToChatClient()
    {
        var sessionId = $"tools-{Guid.NewGuid()}";
        ChatOptions? capturedOptions = null;

        var responseMessage = new ChatMessage(ChatRole.Assistant, "Done");
        var chatResponse = new ChatResponse([responseMessage]) { ModelId = "test" };

        _chatClientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(
                (_, opts, _) => capturedOptions = opts)
            .ReturnsAsync(chatResponse);

        var request = TestHttpRequestHelper.CreatePost(new { message = "list APIs", sessionId });

        await _sut.Chat(request);

        Assert.NotNull(capturedOptions);
        Assert.NotNull(capturedOptions!.Tools);
        Assert.Equal(7, capturedOptions.Tools.Count); // 5 original + 2 new tools
    }
}
