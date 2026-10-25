using System;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.Core.Models.Weather;
using ClassIsland.Helpers;

namespace ClassIsland.Services.WeatherProviders;

public sealed class XiaomiWeatherProvider : IWeatherProvider
{
    public string Id => "xiaomi";

    public Task<WeatherInfo> GetWeatherAsync(WeatherProviderContext context, CancellationToken cancellationToken = default)
    {
        var schema = context.UseHttp ? "http" : "https";
        var uri = $"{schema}://weatherapi.market.xiaomi.com/wtr-v3/weather/all?latitude={context.Latitude}&longitude={context.Longitude}&locationKey={Uri.EscapeDataString(context.CityId)}&days=15&appKey=weather20151024&sign=zUFJoAR2ZVrDy1vF3D07&isGlobal=false&locale=zh_cn";
        return WebRequestHelper.Default.GetJson<WeatherInfo>(new Uri(uri));
    }
}
