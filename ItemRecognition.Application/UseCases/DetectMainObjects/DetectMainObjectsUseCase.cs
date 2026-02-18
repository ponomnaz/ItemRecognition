using ItemRecognition.Application.Common.Ai;
using ItemRecognition.Application.Ports;
using ItemRecognition.Application.Prompts;
using ItemRecognition.Domain.Entities;
using ItemRecognition.Domain.Enums;

namespace ItemRecognition.Application.UseCases.DetectMainObjects;

public sealed class DetectMainObjectsUseCase(
    IRecognitionRequestRepository recognitionRequestRepository,
    IAiCallRepository aiCallRepository,
    IPredictedObjectRepository predictedObjectRepository,
    IImageDownloader imageDownloader,
    IImageHasher imageHasher,
    IImageStorage imageStorage,
    IAiVisionClient aiVisionClient,
    IUnitOfWork unitOfWork)
    : IDetectMainObjectsUseCase
{
    public async Task<DetectMainObjectsResponse> ExecuteAsync(
        DetectMainObjectsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var imageUrl = request.ImageUrl?.Trim();
        if (string.IsNullOrWhiteSpace(imageUrl) ||
            !Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri))
        {
            throw new ArgumentException("Image URL must be an absolute URL.", nameof(request));
        }

        var recognitionRequest = new RecognitionRequest
        {
            ImageUrl = imageUrl,
            Status = RequestStatus.Created
        };

        await recognitionRequestRepository.AddAsync(recognitionRequest, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            var image = await imageDownloader.DownloadAsync(imageUri, cancellationToken);
            recognitionRequest.ImageHash = imageHasher.ComputeSha256Hex(image.Content);
            recognitionRequest.ImageStorageKey = (await imageStorage.SaveAsync(image, cancellationToken)).StorageKey;
            recognitionRequestRepository.Update(recognitionRequest);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var aiResult = await aiVisionClient.DetectMainObjectsAsync(
                recognitionRequest.Id,
                image,
                PromptVersion.MainObjectsV1,
                MainObjectsPrompt.Build(),
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
                AiStage.MainObjects,
                cancellationToken);

            if (aiCall is null)
            {
                return await MarkFailedAsync(
                    recognitionRequest,
                    "ai_call_missing",
                    "AI call log entry was not found for successful MAIN_OBJECTS stage.",
                    cancellationToken);
            }

            var predictions = NormalizePredictions(aiResult.Value);
            if (predictions.Count == 0)
            {
                return await MarkFailedAsync(
                    recognitionRequest,
                    "ai_empty_predictions",
                    "AI returned empty predictions list.",
                    cancellationToken);
            }

            var predictedObjects = predictions
                .Select(prediction => new PredictedObject
                {
                    RequestId = recognitionRequest.Id,
                    AiCallId = aiCall.Id,
                    Name = prediction.Name,
                    IsPrimary = prediction.IsPrimary,
                    Confidence = prediction.Confidence,
                    Rank = prediction.Rank
                })
                .ToArray();

            await predictedObjectRepository.AddRangeAsync(predictedObjects, cancellationToken);

            recognitionRequest.Status = RequestStatus.MainDetected;
            recognitionRequestRepository.Update(recognitionRequest);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new DetectMainObjectsResponse(
                recognitionRequest.Id,
                recognitionRequest.Status,
                predictions,
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

    private async Task<DetectMainObjectsResponse> MarkFailedAsync(
        RecognitionRequest recognitionRequest,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        recognitionRequest.Status = RequestStatus.Failed;
        recognitionRequestRepository.Update(recognitionRequest);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new DetectMainObjectsResponse(
            recognitionRequest.Id,
            recognitionRequest.Status,
            [],
            errorCode,
            errorMessage);
    }

    private static IReadOnlyList<MainObjectPrediction> NormalizePredictions(
        IReadOnlyList<MainObjectPrediction> predictions)
    {
        if (predictions.Count == 0)
        {
            return [];
        }

        var normalized = new List<MainObjectPrediction>(predictions.Count);
        var fallbackRank = 1;

        foreach (var prediction in predictions)
        {
            var normalizedName = prediction.Name?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                fallbackRank++;
                continue;
            }

            var normalizedRank = prediction.Rank >= 1 ? prediction.Rank : fallbackRank;
            var normalizedConfidence = prediction.Confidence is >= 0 and <= 1
                ? prediction.Confidence
                : null;

            normalized.Add(new MainObjectPrediction(
                normalizedName,
                prediction.IsPrimary,
                normalizedConfidence,
                normalizedRank));

            fallbackRank++;
        }

        return normalized
            .OrderBy(prediction => prediction.Rank)
            .ToArray();
    }
}
