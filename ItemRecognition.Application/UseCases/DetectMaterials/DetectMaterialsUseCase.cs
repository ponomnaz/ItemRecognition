using ItemRecognition.Application.Common.Ai;
using ItemRecognition.Application.Ports;
using ItemRecognition.Application.Prompts;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Application.UseCases.DetectMaterials;

public sealed class DetectMaterialsUseCase(
    IRecognitionRequestRepository recognitionRequestRepository,
    IAiCallRepository aiCallRepository,
    IConfirmedObjectRepository confirmedObjectRepository,
    IMaterialRepository materialRepository,
    IConfirmedObjectMaterialRepository confirmedObjectMaterialRepository,
    IImageDownloader imageDownloader,
    IAiVisionClient aiVisionClient,
    IUnitOfWork unitOfWork)
    : IDetectMaterialsUseCase
{
    public async Task<DetectMaterialsResponse> ExecuteAsync(
        DetectMaterialsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.RequestId == Guid.Empty)
        {
            throw new ArgumentException("Request id cannot be empty.", nameof(request));
        }

        var normalizedItemNames = NormalizeItemNames(request.ItemNames);
        if (normalizedItemNames.Count == 0)
        {
            throw new ArgumentException("At least one item name is required.", nameof(request));
        }

        var recognitionRequest = await recognitionRequestRepository.GetByIdAsync(
            request.RequestId,
            cancellationToken);

        if (recognitionRequest is null)
        {
            return new DetectMaterialsResponse(
                request.RequestId,
                RequestStatus.Failed,
                [],
                "request_not_found",
                "Recognition request was not found.");
        }

        if (!CanProcessMaterials(recognitionRequest.Status))
        {
            return new DetectMaterialsResponse(
                recognitionRequest.Id,
                recognitionRequest.Status,
                [],
                "invalid_request_status",
                $"Request status '{recognitionRequest.Status}' does not allow materials detection.");
        }

        if (!Uri.TryCreate(recognitionRequest.ImageUrl, UriKind.Absolute, out var imageUri))
        {
            return await MarkFailedAsync(
                recognitionRequest,
                "invalid_image_url",
                "Stored image URL is invalid.",
                cancellationToken);
        }

        try
        {
            var image = await imageDownloader.DownloadAsync(imageUri, cancellationToken);
            var promptText = MaterialsPrompt.Build(normalizedItemNames);

            var aiResult = await aiVisionClient.DetectMaterialsAsync(
                recognitionRequest.Id,
                image,
                normalizedItemNames,
                PromptVersion.MaterialsV3,
                promptText,
                cancellationToken);

            if (!aiResult.IsSuccess || aiResult.Value is null)
            {
                return await MarkFailedAsync(
                    recognitionRequest,
                    aiResult.Error?.Code ?? "ai_detection_failed",
                    aiResult.Error?.Message ?? "AI did not return a successful result.",
                    cancellationToken);
            }

            var aiCall = await aiCallRepository.GetLatestByRequestAndStageAsync(
                recognitionRequest.Id,
                AiStage.Materials,
                cancellationToken);

            if (aiCall is null)
            {
                return await MarkFailedAsync(
                    recognitionRequest,
                    "ai_call_missing",
                    "AI call log entry was not found for successful MATERIALS stage.",
                    cancellationToken);
            }

            var confirmedObjects = await UpsertConfirmedObjectsAsync(
                recognitionRequest,
                aiCall.Id,
                normalizedItemNames,
                cancellationToken);

            recognitionRequest.Status = RequestStatus.Confirmed;
            recognitionRequestRepository.Update(recognitionRequest);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var normalizedPredictions = NormalizePredictions(aiResult.Value, normalizedItemNames);

            var materialByName = await UpsertMaterialsAsync(
                normalizedPredictions.SelectMany(prediction => prediction.Materials).ToArray(),
                cancellationToken);

            await UpsertConfirmedObjectMaterialsAsync(
                confirmedObjects,
                normalizedPredictions,
                materialByName,
                cancellationToken);

            recognitionRequest.Status = RequestStatus.MaterialsDetected;
            recognitionRequestRepository.Update(recognitionRequest);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var responseItems = BuildResponseItems(normalizedItemNames, normalizedPredictions);

            return new DetectMaterialsResponse(
                recognitionRequest.Id,
                recognitionRequest.Status,
                responseItems,
                null,
                null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await MarkFailedAsync(
                recognitionRequest,
                "request_processing_failed",
                ex.Message,
                cancellationToken);
        }
    }

    private async Task<Dictionary<string, ConfirmedObject>> UpsertConfirmedObjectsAsync(
        RecognitionRequest recognitionRequest,
        Guid aiCallId,
        IReadOnlyList<string> normalizedItemNames,
        CancellationToken cancellationToken)
    {
        var existingObjects = await confirmedObjectRepository.GetByRequestIdAsync(
            recognitionRequest.Id,
            cancellationToken);
        var objectByName = existingObjects.ToDictionary(
            obj => obj.Name,
            obj => obj,
            StringComparer.OrdinalIgnoreCase);

        var toInsert = new List<ConfirmedObject>();
        foreach (var itemName in normalizedItemNames)
        {
            if (objectByName.ContainsKey(itemName))
            {
                continue;
            }

            var confirmedObject = new ConfirmedObject
            {
                RequestId = recognitionRequest.Id,
                AiCallId = aiCallId,
                Name = itemName
            };

            toInsert.Add(confirmedObject);
            objectByName[itemName] = confirmedObject;
        }

        if (toInsert.Count > 0)
        {
            await confirmedObjectRepository.AddRangeAsync(toInsert, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return objectByName;
    }

    private async Task<Dictionary<string, Material>> UpsertMaterialsAsync(
        IReadOnlyCollection<string> materialNames,
        CancellationToken cancellationToken)
    {
        if (materialNames.Count == 0)
        {
            return new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedMaterialNames = materialNames
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedMaterialNames.Length == 0)
        {
            return new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        }

        var existingMaterials = await materialRepository.GetByNamesAsync(normalizedMaterialNames, cancellationToken);
        var materialByName = existingMaterials.ToDictionary(
            material => material.Name,
            material => material,
            StringComparer.OrdinalIgnoreCase);

        var newMaterials = normalizedMaterialNames
            .Where(name => !materialByName.ContainsKey(name))
            .Select(name => new Material { Name = name })
            .ToArray();

        if (newMaterials.Length > 0)
        {
            await materialRepository.AddRangeAsync(newMaterials, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            foreach (var material in newMaterials)
            {
                materialByName[material.Name] = material;
            }
        }

        return materialByName;
    }

    private async Task UpsertConfirmedObjectMaterialsAsync(
        IReadOnlyDictionary<string, ConfirmedObject> confirmedObjectsByName,
        IReadOnlyList<MaterialsPrediction> predictions,
        IReadOnlyDictionary<string, Material> materialsByName,
        CancellationToken cancellationToken)
    {
        if (confirmedObjectsByName.Count == 0 || predictions.Count == 0 || materialsByName.Count == 0)
        {
            return;
        }

        var confirmedObjectIds = confirmedObjectsByName.Values
            .Select(obj => obj.Id)
            .Distinct()
            .ToArray();

        var existingLinks = await confirmedObjectMaterialRepository.GetByConfirmedObjectIdsAsync(
            confirmedObjectIds,
            cancellationToken);

        var existingPairs = existingLinks
            .Select(link => (link.ConfirmedObjectId, link.MaterialId))
            .ToHashSet();

        var linksToInsert = new List<ConfirmedObjectMaterial>();

        foreach (var prediction in predictions)
        {
            if (!confirmedObjectsByName.TryGetValue(prediction.ItemName, out var confirmedObject))
            {
                continue;
            }

            foreach (var materialName in prediction.Materials)
            {
                if (!materialsByName.TryGetValue(materialName, out var material))
                {
                    continue;
                }

                var pair = (confirmedObject.Id, material.Id);
                if (existingPairs.Contains(pair))
                {
                    continue;
                }

                existingPairs.Add(pair);
                linksToInsert.Add(
                    new ConfirmedObjectMaterial
                    {
                        ConfirmedObjectId = confirmedObject.Id,
                        MaterialId = material.Id
                    });
            }
        }

        if (linksToInsert.Count > 0)
        {
            await confirmedObjectMaterialRepository.AddRangeAsync(linksToInsert, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<DetectMaterialsResponse> MarkFailedAsync(
        RecognitionRequest recognitionRequest,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        recognitionRequest.Status = RequestStatus.Failed;
        recognitionRequestRepository.Update(recognitionRequest);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DetectMaterialsResponse(
            recognitionRequest.Id,
            recognitionRequest.Status,
            [],
            errorCode,
            errorMessage);
    }

    private static IReadOnlyList<string> NormalizeItemNames(IReadOnlyList<string>? itemNames)
    {
        if (itemNames is null || itemNames.Count == 0)
        {
            return [];
        }

        return itemNames
            .Select(item => item?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static IReadOnlyList<MaterialsPrediction> NormalizePredictions(
        IReadOnlyList<MaterialsPrediction> predictions,
        IReadOnlyCollection<string> allowedItemNames)
    {
        if (predictions.Count == 0)
        {
            return [];
        }

        var allowed = new HashSet<string>(allowedItemNames, StringComparer.OrdinalIgnoreCase);
        var byItemName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var seenByItemName = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var prediction in predictions)
        {
            var itemName = prediction.ItemName?.Trim();
            if (string.IsNullOrWhiteSpace(itemName) || !allowed.Contains(itemName))
            {
                continue;
            }

            if (!byItemName.TryGetValue(itemName, out var materials))
            {
                materials = [];
                byItemName[itemName] = materials;
                seenByItemName[itemName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var seen = seenByItemName[itemName];
            foreach (var material in prediction.Materials)
            {
                var normalizedMaterial = material?.Trim();
                if (string.IsNullOrWhiteSpace(normalizedMaterial))
                {
                    continue;
                }

                if (!seen.Add(normalizedMaterial))
                {
                    continue;
                }

                materials.Add(normalizedMaterial);
            }
        }

        return byItemName
            .Select(pair => new MaterialsPrediction(pair.Key, pair.Value))
            .OrderBy(prediction => prediction.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<MaterialsPrediction> BuildResponseItems(
        IReadOnlyList<string> normalizedItemNames,
        IReadOnlyList<MaterialsPrediction> predictions)
    {
        var predictionByItemName = predictions.ToDictionary(
            prediction => prediction.ItemName,
            prediction => prediction,
            StringComparer.OrdinalIgnoreCase);

        return normalizedItemNames
            .Select(itemName => predictionByItemName.TryGetValue(itemName, out var prediction)
                ? prediction
                : new MaterialsPrediction(itemName, Array.Empty<string>()))
            .ToArray();
    }

    private static bool CanProcessMaterials(RequestStatus status) =>
        status is RequestStatus.MainDetected or RequestStatus.Confirmed or RequestStatus.Failed;
}
