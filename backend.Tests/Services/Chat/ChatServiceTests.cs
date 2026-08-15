using backend.Models.Database;
using backend.Repositories;
using backend.Services;
using Moq;

namespace backend.Tests.Services.Chat;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task CreateConversationAsync_WhenTitleIsValid_AddsNormalizedConversation()
    {
        // Arrange
        var context = CreateContext();
        var userId = Guid.NewGuid();
        ChatConversation? capturedConversation = null;

        context.ConversationRepository
            .Setup(repository => repository.AddAsync(It.IsAny<ChatConversation>(), It.IsAny<CancellationToken>()))
            .Callback<ChatConversation, CancellationToken>((conversation, _) => capturedConversation = conversation)
            .ReturnsAsync((ChatConversation conversation, CancellationToken _) =>
            {
                conversation.ConversationId = Guid.NewGuid();
                return conversation;
            });

        // Act
        var result = await context.Service.CreateConversationAsync(userId, "  Morning commute  ");

        // Assert
        Assert.NotNull(result);
        Assert.Same(capturedConversation, result);
        Assert.Equal(userId, capturedConversation?.UserId);
        Assert.Equal("Morning commute", capturedConversation?.Title);
        Assert.NotEqual(default, capturedConversation?.CreatedAt);
        Assert.NotEqual(default, capturedConversation?.UpdatedAt);

        context.ConversationRepository.Verify(
            repository => repository.AddAsync(It.IsAny<ChatConversation>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateConversationAsync_WhenTitleIsTooLong_ReturnsNullWithoutAddingConversation()
    {
        // Arrange
        var context = CreateContext();
        var title = new string('x', 201);

        // Act
        var result = await context.Service.CreateConversationAsync(Guid.NewGuid(), title);

        // Assert
        Assert.Null(result);
        context.ConversationRepository.Verify(
            repository => repository.AddAsync(It.IsAny<ChatConversation>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetConversationDetailsAsync_WhenConversationExists_ReturnsConversationWithMessages()
    {
        // Arrange
        var context = CreateContext();
        var conversationId = Guid.NewGuid();
        var conversation = new ChatConversation
        {
            ConversationId = conversationId,
            UserId = Guid.NewGuid(),
            Title = "Trip planning",
            CreatedAt = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 6, 1, 8, 5, 0, DateTimeKind.Utc),
        };
        var messages = new List<ChatMessage>
        {
            new()
            {
                MessageId = Guid.NewGuid(),
                ConversationId = conversationId,
                Sender = "user",
                Message = "How do I get to BGC?",
                CreatedAt = new DateTime(2026, 6, 1, 8, 1, 0, DateTimeKind.Utc),
            },
            new()
            {
                MessageId = Guid.NewGuid(),
                ConversationId = conversationId,
                Sender = "assistant",
                Message = "Take the bus.",
                CreatedAt = new DateTime(2026, 6, 1, 8, 2, 0, DateTimeKind.Utc),
            },
        };

        context.ConversationRepository
            .Setup(repository => repository.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);
        context.MessageRepository
            .Setup(repository => repository.GetByConversationAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messages);

        // Act
        var result = await context.Service.GetConversationDetailsAsync(conversationId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(conversationId, result.ConversationId);
        Assert.Equal("Trip planning", result.Title);
        Assert.Equal(["user", "assistant"], result.Messages.Select(message => message.Sender));
        Assert.Equal("How do I get to BGC?", result.Messages[0].Message);

        context.ConversationRepository.Verify(
            repository => repository.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()),
            Times.Once);
        context.MessageRepository.Verify(
            repository => repository.GetByConversationAsync(conversationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddMessageAsync_WhenConversationAndTripSearchExist_AddsMessageAndTouchesConversationTitle()
    {
        // Arrange
        var context = CreateContext();
        var conversationId = Guid.NewGuid();
        var tripSearchId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 6, 1, 8, 10, 0, DateTimeKind.Utc);
        ChatMessage? capturedMessage = null;

        context.ConversationRepository
            .Setup(repository => repository.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatConversation
            {
                ConversationId = conversationId,
                UserId = Guid.NewGuid(),
                Title = "Trip planning",
            });
        context.TripSearchRepository
            .Setup(repository => repository.GetByIdAsync(tripSearchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TripSearch { TripSearchId = tripSearchId });
        context.MessageRepository
            .Setup(repository => repository.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ChatMessage, CancellationToken>((message, _) => capturedMessage = message)
            .ReturnsAsync((ChatMessage message, CancellationToken _) =>
            {
                message.MessageId = Guid.NewGuid();
                return message;
            });
        context.ConversationRepository
            .Setup(repository => repository.UpdateTitleAsync(conversationId, "Trip planning", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await context.Service.AddMessageAsync(
            conversationId,
            "  user  ",
            "  Need a route  ",
            extractedBudget: 150,
            extractedOrigin: "  Ayala  ",
            extractedDestination: "  BGC  ",
            tripSearchId: tripSearchId,
            createdAt: createdAt);

        // Assert
        Assert.NotNull(result);
        Assert.Same(capturedMessage, result);
        Assert.Equal(conversationId, capturedMessage?.ConversationId);
        Assert.Equal("user", capturedMessage?.Sender);
        Assert.Equal("Need a route", capturedMessage?.Message);
        Assert.Equal(150, capturedMessage?.ExtractedBudget);
        Assert.Equal("Ayala", capturedMessage?.ExtractedOrigin);
        Assert.Equal("BGC", capturedMessage?.ExtractedDestination);
        Assert.Equal(tripSearchId, capturedMessage?.TripSearchId);
        Assert.Equal(createdAt, capturedMessage?.CreatedAt);

        context.MessageRepository.Verify(
            repository => repository.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
        context.ConversationRepository.Verify(
            repository => repository.UpdateTitleAsync(conversationId, "Trip planning", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddMessageAsync_WhenConversationDoesNotExist_ReturnsNullWithoutAddingMessage()
    {
        // Arrange
        var context = CreateContext();
        var conversationId = Guid.NewGuid();

        context.ConversationRepository
            .Setup(repository => repository.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatConversation?)null);

        // Act
        var result = await context.Service.AddMessageAsync(conversationId, "user", "Hello");

        // Assert
        Assert.Null(result);
        context.MessageRepository.Verify(
            repository => repository.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddMessageAsync_WhenTripSearchDoesNotExist_ReturnsNullWithoutAddingMessage()
    {
        // Arrange
        var context = CreateContext();
        var conversationId = Guid.NewGuid();
        var tripSearchId = Guid.NewGuid();

        context.ConversationRepository
            .Setup(repository => repository.GetByIdAsync(conversationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatConversation { ConversationId = conversationId, UserId = Guid.NewGuid() });
        context.TripSearchRepository
            .Setup(repository => repository.GetByIdAsync(tripSearchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TripSearch?)null);

        // Act
        var result = await context.Service.AddMessageAsync(conversationId, "user", "Hello", tripSearchId: tripSearchId);

        // Assert
        Assert.Null(result);
        context.MessageRepository.Verify(
            repository => repository.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AddMessageAsync_WhenSenderIsTooLong_ReturnsNullWithoutTouchingRepositories()
    {
        // Arrange
        var context = CreateContext();
        var sender = new string('x', 21);

        // Act
        var result = await context.Service.AddMessageAsync(Guid.NewGuid(), sender, "Hello");

        // Assert
        Assert.Null(result);
        context.ConversationRepository.Verify(
            repository => repository.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        context.MessageRepository.Verify(
            repository => repository.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateConversationTitleAsync_WhenTitleIsValid_DelegatesWithNormalizedTitle()
    {
        // Arrange
        var context = CreateContext();
        var conversationId = Guid.NewGuid();

        context.ConversationRepository
            .Setup(repository => repository.UpdateTitleAsync(conversationId, "New title", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await context.Service.UpdateConversationTitleAsync(conversationId, "  New title  ");

        // Assert
        Assert.True(result);
        context.ConversationRepository.Verify(
            repository => repository.UpdateTitleAsync(conversationId, "New title", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TestContext CreateContext()
    {
        var conversationRepository = new Mock<IChatConversationRepository>(MockBehavior.Strict);
        var messageRepository = new Mock<IChatMessageRepository>(MockBehavior.Strict);
        var tripSearchRepository = new Mock<ITripSearchRepository>(MockBehavior.Strict);

        return new TestContext(
            new ChatService(
                conversationRepository.Object,
                messageRepository.Object,
                tripSearchRepository.Object),
            conversationRepository,
            messageRepository,
            tripSearchRepository);
    }

    private sealed record TestContext(
        ChatService Service,
        Mock<IChatConversationRepository> ConversationRepository,
        Mock<IChatMessageRepository> MessageRepository,
        Mock<ITripSearchRepository> TripSearchRepository);
}
