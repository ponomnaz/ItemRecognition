using FluentValidation;
using FluentValidation.Results;
using ItemRecognition.Api.Contracts.Recognition;
using ItemRecognition.Api.Validation;
using ItemRecognition.Application.Ports;
using ItemRecognition.Application.UseCases.DetectMainObjects;
using ItemRecognition.Application.UseCases.DetectMaterials;
using ItemRecognition.Application.UseCases.GetAnalyticsSummary;
using ItemRecognition.Application.UseCases.GetAnonymizedExport;
using ItemRecognition.Infrastructure.Ai;
using ItemRecognition.Infrastructure.Images;
using ItemRecognition.Infrastructure.Persistence;
using ItemRecognition.Infrastructure.Repositories;
using ItemRecognition.Infrastructure.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ItemRecognition.Api;

public class Startup(IConfiguration configuration)
{
    public IConfiguration Configuration { get; } = configuration;

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var failures = context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .SelectMany(entry => entry.Value!.Errors.Select(error =>
                    {
                        var message = string.IsNullOrWhiteSpace(error.ErrorMessage)
                            ? "Request value is invalid."
                            : error.ErrorMessage;
                        var target = string.IsNullOrWhiteSpace(entry.Key) ? "request" : entry.Key;

                        return new ValidationFailure(target, message);
                    }))
                    .ToArray();

                var error = ApiErrorResponseFactory.FromValidationFailures(
                    failures,
                    context.HttpContext.TraceIdentifier);

                return new BadRequestObjectResult(error);
            };
        });

        var connectionString = Configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
        }

        services.AddDbContext<ItemRecognitionDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsqlOptions => npgsqlOptions.MapItemRecognitionEnums()));

        var imageOptions = new ImageProcessingOptions();
        Configuration.GetSection("ImageProcessing").Bind(imageOptions);

        services.AddSingleton(imageOptions);
        services.AddHttpClient<IImageDownloader, HttpImageDownloader>();
        services.AddSingleton<IImageHasher, Sha256ImageHasher>();
        services.AddSingleton<IImageStorage, LocalImageStorage>();

        services.AddScoped<IRecognitionRequestRepository, RecognitionRequestRepository>();
        services.AddScoped<IAiCallRepository, AiCallRepository>();
        services.AddScoped<IPredictedObjectRepository, PredictedObjectRepository>();
        services.AddScoped<IConfirmedObjectRepository, ConfirmedObjectRepository>();
        services.AddScoped<IMaterialRepository, MaterialRepository>();
        services.AddScoped<IConfirmedObjectMaterialRepository, ConfirmedObjectMaterialRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IDetectMainObjectsUseCase, DetectMainObjectsUseCase>();
        services.AddScoped<IDetectMaterialsUseCase, DetectMaterialsUseCase>();
        services.AddScoped<IGetAnonymizedExportUseCase, GetAnonymizedExportUseCase>();
        services.AddScoped<IGetAnalyticsSummaryUseCase, GetAnalyticsSummaryUseCase>();

        services.AddScoped<IAnonymizedExportQueryService, AnonymizedExportQueryService>();
        services.AddScoped<IAnalyticsSummaryQueryService, AnalyticsSummaryQueryService>();

        services.AddGigaChatAiVisionClient(options =>
        {
            Configuration.GetSection(GigaChatOptions.SectionName).Bind(options);
        });

        services.AddValidatorsFromAssemblyContaining<CreateRecognitionRequestDtoValidator>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseAuthorization();

        app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
    }
}
