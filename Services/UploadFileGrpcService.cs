using Grpc.Core;
using Libs.Core.Public.Protos.Upload.File.Service;

namespace Upload.File.Service.Services;

public sealed class UploadFileGrpcService(
    IHttpContextAccessor httpContextAcc,
    ILogger<UploadFileGrpcService> logger
) : UploadFileGrpc.UploadFileGrpcBase
{
    private readonly IHttpContextAccessor _httpContextAcc = httpContextAcc;
    private readonly ILogger<UploadFileGrpcService> _logger = logger;

    private static readonly string[] AllowedImageExtensions =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tiff"
    ];

    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/bmp",
        "image/tiff"
    ];

    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public override async Task<GrpcUploadFlyerResponse> UploadFlyer(
    IAsyncStreamReader<GrpcUploadFlyerRequest> requestStream,
    ServerCallContext context)
    {
        long totalBytes = 0;
        FileStream? fileStream = null;
        string? savedFileName = null;

        try
        {
            await foreach (var chunk in requestStream.ReadAllAsync(context.CancellationToken))
            {
                // Primeiro chunk → metadados + validações
                if (fileStream is null)
                {
                    string? originalFileName = chunk.FileName;
                    string? contentType = chunk.ContentType;

                    if (string.IsNullOrWhiteSpace(originalFileName))
                        return new GrpcUploadFlyerResponse { IsSuccess = false, Message = "No file uploaded or file is empty." };

                    string? extension = Path.GetExtension(originalFileName).ToLowerInvariant();

                    if (!AllowedImageExtensions.Contains(extension))
                        return new GrpcUploadFlyerResponse { IsSuccess = false, Message = $"File format not allowed. Images only: {string.Join(", ", AllowedImageExtensions)}" };

                    if (string.IsNullOrWhiteSpace(contentType) ||
                        !AllowedContentTypes.Contains(contentType.ToLowerInvariant()))
                        return new GrpcUploadFlyerResponse { IsSuccess = false, Message = "Invalid content type. Only image files are permitted." };

                    var uploadFolder = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "flyers"
                    );

                    Directory.CreateDirectory(uploadFolder);

                    savedFileName = $"{Guid.NewGuid():N}{extension}";
                    var filePath = Path.Combine(uploadFolder, savedFileName);

                    fileStream = new FileStream(
                        filePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 64 * 1024,
                        useAsync: true
                    );
                }

                totalBytes += chunk.Content!.Length;

                if (totalBytes > MaxFileSizeBytes)
                    return new GrpcUploadFlyerResponse { IsSuccess = false, Message = $"The file exceeds the maximum allowed size ({MaxFileSizeBytes / 1024 / 1024} MB)" };

                await fileStream.WriteAsync(
                    chunk.Content.Memory,
                    context.CancellationToken
                );
            }

            if (fileStream is null || totalBytes == 0)
                return new GrpcUploadFlyerResponse { IsSuccess = false, Message = "No file uploaded or file is empty." };

            await fileStream.FlushAsync(context.CancellationToken);

            var request = _httpContextAcc.HttpContext!.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            var fullUrl = $"{baseUrl}/flyers/{savedFileName}";

            return new GrpcUploadFlyerResponse
            {
                IsSuccess = true,
                Message = fullUrl
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Upload cancelled by client.");
            return new GrpcUploadFlyerResponse { IsSuccess = false, Message = "Upload cancelled." };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: UploadFileGrpcService -> UploadFlyer");
            return new GrpcUploadFlyerResponse { IsSuccess = false, Message = "Error saving the image." };
        }
        finally
        {
            if (fileStream is not null)
                await fileStream.DisposeAsync();
        }
    }
}