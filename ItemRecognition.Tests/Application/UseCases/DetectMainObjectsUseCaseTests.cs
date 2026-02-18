using ItemRecognition.Application.Common.Ai;
using ItemRecognition.Application.Common.Images;
using ItemRecognition.Application.Ports;
using ItemRecognition.Application.UseCases.DetectMainObjects;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Tests.Application.UseCases;

public sealed class DetectMainObjectsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenAiReturnsPredictions_SavesObjectsAndMarksMainDetected()
    {
        var requestRepository = new InMemoryRecognitionRequestRepository();
        var aiCallRepository = new InMemoryAiCallRepository();
        var predictedObjectRepository = new InMemoryPredictedObjectRepository();
        var unitOfWork = new InMemoryUnitOfWork();

        var useCase = new DetectMainObjectsUseCase(
            requestRepository,
            aiCallRepository,
            predictedObjectRepository,
            new StubImageDownloader(),
            new StubImageHasher(),
            new StubImageStorage(),
            new StubAiVisionClient(aiCallRepository, shouldFail: false),
            unitOfWork);

        var response = await useCase.ExecuteAsync(
            new DetectMainObjectsRequest("https://example.com/image.jpg"));

        Assert.True(response.IsSuccess);
        Assert.Equal(RequestStatus.MainDetected, response.Status);
        Assert.NotEqual(Guid.Empty, response.RequestId);
        Assert.Single(response.Objects);

        var savedRequest = Assert.Single(requestRepository.Requests);
        Assert.Equal(RequestStatus.MainDetected, savedRequest.Status);
        Assert.Equal("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", savedRequest.ImageHash);
        Assert.Equal("stored-image.jpg", savedRequest.ImageStorageKey);

        var savedPrediction = Assert.Single(predictedObjectRepository.Objects);
        Assert.Equal(response.RequestId, savedPrediction.RequestId);
        Assert.NotEqual(Guid.Empty, savedPrediction.AiCallId);
        Assert.Equal("стол", savedPrediction.Name);
        Assert.True(savedPrediction.IsPrimary);
        Assert.Equal(1, savedPrediction.Rank);

        Assert.Single(aiCallRepository.Calls);
        Assert.True(unitOfWork.SaveChangesCalls >= 3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAiFails_MarksRequestFailedAndReturnsError()
    {
        var requestRepository = new InMemoryRecognitionRequestRepository();
        var aiCallRepository = new InMemoryAiCallRepository();
        var predictedObjectRepository = new InMemoryPredictedObjectRepository();

        var useCase = new DetectMainObjectsUseCase(
            requestRepository,
            aiCallRepository,
            predictedObjectRepository,
            new StubImageDownloader(),
            new StubImageHasher(),
            new StubImageStorage(),
            new StubAiVisionClient(aiCallRepository, shouldFail: true),
            new InMemoryUnitOfWork());

        var response = await useCase.ExecuteAsync(
            new DetectMainObjectsRequest("https://example.com/image.jpg"));

        Assert.False(response.IsSuccess);
        Assert.Equal(RequestStatus.Failed, response.Status);
        Assert.Equal("ai_failed", response.ErrorCode);
        Assert.Empty(response.Objects);

        var savedRequest = Assert.Single(requestRepository.Requests);
        Assert.Equal(RequestStatus.Failed, savedRequest.Status);
        Assert.Equal("stored-image.jpg", savedRequest.ImageStorageKey);
        Assert.Empty(predictedObjectRepository.Objects);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUrlIsInvalid_ThrowsArgumentException()
    {
        var useCase = new DetectMainObjectsUseCase(
            new InMemoryRecognitionRequestRepository(),
            new InMemoryAiCallRepository(),
            new InMemoryPredictedObjectRepository(),
            new StubImageDownloader(),
            new StubImageHasher(),
            new StubImageStorage(),
            new StubAiVisionClient(new InMemoryAiCallRepository(), shouldFail: false),
            new InMemoryUnitOfWork());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(new DetectMainObjectsRequest("not-a-url")));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAiReturnsSeveralMainObjects_ReturnsOrderedList()
    {
        var requestRepository = new InMemoryRecognitionRequestRepository();
        var aiCallRepository = new InMemoryAiCallRepository();
        var predictedObjectRepository = new InMemoryPredictedObjectRepository();

        var aiPredictions = new[]
        {
            new MainObjectPrediction("ручка", true, 0.75f, 2),
            new MainObjectPrediction("ежедневник", true, 0.91f, 1),
            new MainObjectPrediction("фон", false, 0.99f, 1)
        };

        var useCase = new DetectMainObjectsUseCase(
            requestRepository,
            aiCallRepository,
            predictedObjectRepository,
            new StubImageDownloader(),
            new StubImageHasher(),
            new StubImageStorage(),
            new StubAiVisionClient(aiCallRepository, shouldFail: false, aiPredictions),
            new InMemoryUnitOfWork());

        var response = await useCase.ExecuteAsync(
            new DetectMainObjectsRequest("https://example.com/image.jpg"));

        Assert.True(response.IsSuccess);
        Assert.Equal(2, response.Objects.Count);

        Assert.Collection(
            response.Objects,
            first =>
            {
                Assert.Equal("ежедневник", first.Name);
                Assert.True(first.IsPrimary);
                Assert.Equal(1, first.Rank);
            },
            second =>
            {
                Assert.Equal("ручка", second.Name);
                Assert.True(second.IsPrimary);
                Assert.Equal(2, second.Rank);
            });

        Assert.Equal(2, predictedObjectRepository.Objects.Count);
    }

    private sealed class InMemoryRecognitionRequestRepository : IRecognitionRequestRepository
    {
        private readonly List<RecognitionRequest> _requests = [];
        public IReadOnlyList<RecognitionRequest> Requests => _requests;

        public Task<RecognitionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_requests.FirstOrDefault(request => request.Id == id));

        public Task AddAsync(RecognitionRequest request, CancellationToken cancellationToken = default)
        {
            request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
            request.CreatedAt = request.CreatedAt == default ? DateTimeOffset.UtcNow : request.CreatedAt;
            request.UpdatedAt = DateTimeOffset.UtcNow;
            _requests.Add(request);

            return Task.CompletedTask;
        }

        public void Update(RecognitionRequest request) => request.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private sealed class InMemoryAiCallRepository : IAiCallRepository
    {
        private readonly List<AiCall> _calls = [];
        public IReadOnlyList<AiCall> Calls => _calls;

        public Task AddAsync(AiCall aiCall, CancellationToken cancellationToken = default)
        {
            aiCall.Id = aiCall.Id == Guid.Empty ? Guid.NewGuid() : aiCall.Id;
            aiCall.CreatedAt = aiCall.CreatedAt == default ? DateTimeOffset.UtcNow : aiCall.CreatedAt;
            _calls.Add(aiCall);

            return Task.CompletedTask;
        }

        public Task<AiCall?> GetLatestByRequestAndStageAsync(
            Guid requestId,
            AiStage stage,
            CancellationToken cancellationToken = default)
        {
            var latest = _calls
                .Where(call => call.RequestId == requestId && call.Stage == stage)
                .OrderByDescending(call => call.CreatedAt)
                .ThenByDescending(call => call.Id)
                .FirstOrDefault();

            return Task.FromResult(latest);
        }
    }

    private sealed class InMemoryPredictedObjectRepository : IPredictedObjectRepository
    {
        private readonly List<PredictedObject> _objects = [];
        public IReadOnlyList<PredictedObject> Objects => _objects;

        public Task AddRangeAsync(IReadOnlyCollection<PredictedObject> objects, CancellationToken cancellationToken = default)
        {
            _objects.AddRange(objects);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public int SaveChangesCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class StubImageDownloader : IImageDownloader
    {
        public Task<DownloadedImage> DownloadAsync(Uri imageUrl, CancellationToken cancellationToken = default)
        {
            var bytes = new byte[] { 0x01, 0x02, 0x03 };
            return Task.FromResult(new DownloadedImage(bytes, "image/jpeg", bytes.Length));
        }
    }

    private sealed class StubImageHasher : IImageHasher
    {
        public string ComputeSha256Hex(byte[] data) =>
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    }

    private sealed class StubImageStorage : IImageStorage
    {
        public Task<ImageStorageResult> SaveAsync(DownloadedImage image, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImageStorageResult("stored-image.jpg", image.ContentLength, image.ContentType));
    }

    private sealed class StubAiVisionClient(
        InMemoryAiCallRepository aiCallRepository,
        bool shouldFail,
        IReadOnlyList<MainObjectPrediction>? predictions = null)
        : IAiVisionClient
    {
        public async Task<AiResult<IReadOnlyList<MainObjectPrediction>>> DetectMainObjectsAsync(
            Guid requestId,
            DownloadedImage image,
            string promptVersion,
            string promptText,
            CancellationToken cancellationToken = default)
        {
            await aiCallRepository.AddAsync(
                new AiCall
                {
                    RequestId = requestId,
                    Stage = AiStage.MainObjects,
                    Provider = "stub",
                    Model = "stub",
                    PromptVersion = promptVersion,
                    PromptText = promptText,
                    RequestPayload = "{}",
                    ResponseJson = "{}",
                    IsSuccess = !shouldFail,
                    DurationMs = 10
                },
                cancellationToken);

            if (shouldFail)
            {
                return AiResult<IReadOnlyList<MainObjectPrediction>>.Failure("ai_failed", "AI call failed in test.");
            }

            return AiResult<IReadOnlyList<MainObjectPrediction>>.Success(
                predictions ??
                [
                    new MainObjectPrediction("стол", true, 0.94f, 1)
                ]);
        }

        public Task<AiResult<IReadOnlyList<MaterialsPrediction>>> DetectMaterialsAsync(
            Guid requestId,
            DownloadedImage image,
            IReadOnlyList<string> itemNames,
            string promptVersion,
            string promptText,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used in detect main objects tests.");
    }
}
