using Upload.File.Service.src.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureServices();

var app = builder.Build();
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
app.ConfigurePipeline();

app.Run();
