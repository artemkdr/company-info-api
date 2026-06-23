using NLog.Extensions.Logging;
using NLog.Web;

namespace CompanyInfo.Api.Shared.Extensions;

public static class NLogExtensions
{
    public static void AddNLog(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddNLog();
        builder.Host.UseNLog();
    }
}
