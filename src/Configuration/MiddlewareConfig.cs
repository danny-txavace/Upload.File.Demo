using Microsoft.Extensions.FileProviders;
using Upload.File.Service.Services;

namespace Upload.File.Service.src.Configuration;

public static class MiddlewareConfig
{
    public static void ConfigureServices(this IServiceCollection services)
    {
        var logger = LoggerFactory.Create(s => s.AddConsole()).CreateLogger<Program>();

        try
        {
            services.AddHttpContextAccessor();

            services.AddGrpc();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while configuring Middleware");
        }
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {   
        app.UseDefaultFiles();
        app.UseStaticFiles();
        
        string[] folders = ["flyers", "news"];
        foreach (var folder in folders)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folder);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(path),
                RequestPath = "/" + folder
            });
        }

        app.MapGrpcService<UploadFileGrpcService>();

        return app;
    }
}