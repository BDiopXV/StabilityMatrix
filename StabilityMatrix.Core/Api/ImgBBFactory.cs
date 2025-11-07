using Injectio.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refit;

namespace StabilityMatrix.Core.Api;

public static class ImgBBApiFactory
{
    public static IImgBBApi Create(IServiceProvider sp)
    {
        var logger = sp.GetRequiredService<ILogger<LoggingHandler>>();
        var handler = new LoggingHandler(logger) { InnerHandler = new HttpClientHandler() };

        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.imgbb.com/1/") };

        return RestService.For<IImgBBApi>(client);
    }
}
