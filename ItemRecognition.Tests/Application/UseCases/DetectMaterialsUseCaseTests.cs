using ItemRecognition.Application.Common.Ai;
using ItemRecognition.Application.Common.Images;
using ItemRecognition.Application.Ports;
using ItemRecognition.Application.UseCases.DetectMaterials;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Tests.Application.UseCases;

public sealed class DetectMaterialsUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WhenInputIsValid_UpsertsMaterialsAndMarksMaterialsDetected()
    {
        var requestId = Guid.NewGuid();
        var requestRepository = new InMemoryRecognitionRequestRepository(
        [
            new RecognitionRequest
            {
                Id = requestId,
                Status = RequestStatus.MainDetected,
                ImageUrl = "https://example.com/image.jpg",
                ImageHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                ImageStorageKey = "stored-image.jpg"
            }
        ]);

        var aiCallRepository = new InMemoryAiCallRepository();
        var confirmedObjectRepository = new InMemoryConfirmedObjectRepository();
        var materialRepository = new InMemoryMaterialRepository(
        [
            new Material { Id = Guid.NewGuid(), Name = "металл" }
        ]);
        var linkRepository = new InMemoryConfirmedObjectMaterialRepository();

        var useCase = new DetectMaterialsUseCase(
            requestRepository,
            aiCallRepository,
            confirmedObjectRepository,
            materialRepository,
            linkRepository,
            new StubImageDownloader(),
            new StubAiVisionClient(aiCallRepository, shouldFail: false),
            new InMemoryUnitOfWork());

        var response = await useCase.ExecuteAsync(
            new DetectMaterialsRequest(requestId, ["Стол", "Тумба", "стол"]));

        Assert.True(response.IsSuccess);
        Assert.Equal(RequestStatus.MaterialsDetected, response.Status);
        Assert.Equal(requestId, response.RequestId);
        Assert.Equal(2, response.Items.Count);

        var responseItemNames = response.Items.Select(item => item.ItemName).ToArray();
        Assert.Equal(["Стол", "Тумба"], responseItemNames);

        Assert.Equal(RequestStatus.MaterialsDetected, requestRepository.GetRequired(requestId).Status);
        Assert.Equal(2, (await confirmedObjectRepository.GetByRequestIdAsync(requestId)).Count);
        Assert.Equal(2, materialRepository.Materials.Count(material =>
            material.Name.Equals("металл", StringComparison.OrdinalIgnoreCase) ||
            material.Name.Equals("дерево", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(2, linkRepository.Links.Count);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestNotFound_ReturnsFailedResponse()
    {
        var useCase = new DetectMaterialsUseCase(
            new InMemoryRecognitionRequestRepository([]),
            new InMemoryAiCallRepository(),
            new InMemoryConfirmedObjectRepository(),
            new InMemoryMaterialRepository([]),
            new InMemoryConfirmedObjectMaterialRepository(),
            new StubImageDownloader(),
            new StubAiVisionClient(new InMemoryAiCallRepository(), shouldFail: false),
            new InMemoryUnitOfWork());

        var requestId = Guid.NewGuid();
        var response = await useCase.ExecuteAsync(new DetectMaterialsRequest(requestId, ["Стол"]));

        Assert.False(response.IsSuccess);
        Assert.Equal(RequestStatus.Failed, response.Status);
        Assert.Equal("request_not_found", response.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAiFails_MarksRequestFailed()
    {
        var requestId = Guid.NewGuid();
        var requestRepository = new InMemoryRecognitionRequestRepository(
        [
            new RecognitionRequest
            {
                Id = requestId,
                Status = RequestStatus.MainDetected,
                ImageUrl = "https://example.com/image.jpg"
            }
        ]);

        var confirmedObjectRepository = new InMemoryConfirmedObjectRepository();

        var useCase = new DetectMaterialsUseCase(
            requestRepository,
            new InMemoryAiCallRepository(),
            confirmedObjectRepository,
            new InMemoryMaterialRepository([]),
            new InMemoryConfirmedObjectMaterialRepository(),
            new StubImageDownloader(),
            new StubAiVisionClient(new InMemoryAiCallRepository(), shouldFail: true),
            new InMemoryUnitOfWork());

        var response = await useCase.ExecuteAsync(new DetectMaterialsRequest(requestId, ["Стол"]));

        Assert.False(response.IsSuccess);
        Assert.Equal(RequestStatus.Failed, response.Status);
        Assert.Equal("ai_failed", response.ErrorCode);
        Assert.Empty(await confirmedObjectRepository.GetByRequestIdAsync(requestId));
        Assert.Equal(RequestStatus.Failed, requestRepository.GetRequired(requestId).Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenItemsAreEmpty_ThrowsArgumentException()
    {
        var useCase = new DetectMaterialsUseCase(
            new InMemoryRecognitionRequestRepository([]),
            new InMemoryAiCallRepository(),
            new InMemoryConfirmedObjectRepository(),
            new InMemoryMaterialRepository([]),
            new InMemoryConfirmedObjectMaterialRepository(),
            new StubImageDownloader(),
            new StubAiVisionClient(new InMemoryAiCallRepository(), shouldFail: false),
            new InMemoryUnitOfWork());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(new DetectMaterialsRequest(Guid.NewGuid(), [])));
    }

    private sealed class InMemoryRecognitionRequestRepository(IReadOnlyCollection<RecognitionRequest> seed)
        : IRecognitionRequestRepository
    {
        private readonly Dictionary<Guid, RecognitionRequest> _requests = seed.ToDictionary(request => request.Id);

        public Task<RecognitionRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_requests.GetValueOrDefault(id));

        public Task AddAsync(RecognitionRequest request, CancellationToken cancellationToken = default)
        {
            request.Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
            _requests[request.Id] = request;
            return Task.CompletedTask;
        }

        public void Update(RecognitionRequest request) => _requests[request.Id] = request;

        public RecognitionRequest GetRequired(Guid requestId) => _requests[requestId];
    }

    private sealed class InMemoryAiCallRepository : IAiCallRepository
    {
        private readonly List<AiCall> _calls = [];

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
            var call = _calls
                .Where(existing => existing.RequestId == requestId && existing.Stage == stage)
                .OrderByDescending(existing => existing.CreatedAt)
                .ThenByDescending(existing => existing.Id)
                .FirstOrDefault();

            return Task.FromResult(call);
        }
    }

    private sealed class InMemoryConfirmedObjectRepository : IConfirmedObjectRepository
    {
        private readonly List<ConfirmedObject> _objects = [];

        public Task AddRangeAsync(IReadOnlyCollection<ConfirmedObject> objects, CancellationToken cancellationToken = default)
        {
            foreach (var confirmedObject in objects)
            {
                confirmedObject.Id = confirmedObject.Id == Guid.Empty ? Guid.NewGuid() : confirmedObject.Id;
                _objects.Add(confirmedObject);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ConfirmedObject>> GetByRequestIdAsync(
            Guid requestId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ConfirmedObject>>(GetByRequestId(requestId));

        public IReadOnlyList<ConfirmedObject> GetByRequestId(Guid requestId) =>
            _objects.Where(confirmedObject => confirmedObject.RequestId == requestId).ToArray();
    }

    private sealed class InMemoryMaterialRepository(IReadOnlyCollection<Material> seed) : IMaterialRepository
    {
        private readonly List<Material> _materials = [.. seed];

        public IReadOnlyList<Material> Materials => _materials;

        public Task<IReadOnlyList<Material>> GetByNamesAsync(
            IReadOnlyCollection<string> names,
            CancellationToken cancellationToken = default)
        {
            var set = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            var result = _materials
                .Where(material => set.Contains(material.Name))
                .ToArray();

            return Task.FromResult<IReadOnlyList<Material>>(result);
        }

        public Task AddRangeAsync(IReadOnlyCollection<Material> materials, CancellationToken cancellationToken = default)
        {
            foreach (var material in materials)
            {
                material.Id = material.Id == Guid.Empty ? Guid.NewGuid() : material.Id;
                _materials.Add(material);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryConfirmedObjectMaterialRepository : IConfirmedObjectMaterialRepository
    {
        private readonly List<ConfirmedObjectMaterial> _links = [];
        public IReadOnlyList<ConfirmedObjectMaterial> Links => _links;

        public Task<IReadOnlyList<ConfirmedObjectMaterial>> GetByConfirmedObjectIdsAsync(
            IReadOnlyCollection<Guid> confirmedObjectIds,
            CancellationToken cancellationToken = default)
        {
            var ids = new HashSet<Guid>(confirmedObjectIds);
            var result = _links
                .Where(link => ids.Contains(link.ConfirmedObjectId))
                .ToArray();

            return Task.FromResult<IReadOnlyList<ConfirmedObjectMaterial>>(result);
        }

        public Task AddRangeAsync(
            IReadOnlyCollection<ConfirmedObjectMaterial> links,
            CancellationToken cancellationToken = default)
        {
            _links.AddRange(links);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
    }

    private sealed class StubImageDownloader : IImageDownloader
    {
        public Task<DownloadedImage> DownloadAsync(Uri imageUrl, CancellationToken cancellationToken = default)
        {
            var bytes = new byte[] { 0x01, 0x02, 0x03 };
            return Task.FromResult(new DownloadedImage(bytes, "image/jpeg", bytes.Length));
        }
    }

    private sealed class StubAiVisionClient(InMemoryAiCallRepository aiCallRepository, bool shouldFail) : IAiVisionClient
    {
        public Task<AiResult<IReadOnlyList<MainObjectPrediction>>> DetectMainObjectsAsync(
            Guid requestId,
            DownloadedImage image,
            string promptVersion,
            string promptText,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used in detect materials tests.");

        public async Task<AiResult<IReadOnlyList<MaterialsPrediction>>> DetectMaterialsAsync(
            Guid requestId,
            DownloadedImage image,
            IReadOnlyList<string> itemNames,
            string promptVersion,
            string promptText,
            CancellationToken cancellationToken = default)
        {
            await aiCallRepository.AddAsync(
                new AiCall
                {
                    RequestId = requestId,
                    Stage = AiStage.Materials,
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
                return AiResult<IReadOnlyList<MaterialsPrediction>>.Failure("ai_failed", "AI call failed in test.");
            }

            return AiResult<IReadOnlyList<MaterialsPrediction>>.Success(
            [
                new MaterialsPrediction("Стол", ["металл", "металл"]),
                new MaterialsPrediction("Тумба", ["дерево"]),
                new MaterialsPrediction("Посторонний", ["пластик"])
            ]);
        }
    }
}
