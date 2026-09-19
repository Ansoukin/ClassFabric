using System.Threading;
using System.Threading.Tasks;
using ClassIsland.Core.Models.Weather;

namespace ClassIsland.Services.WeatherProviders;

public interface IWeatherProvider
{
    string Id { get; }

    Task<WeatherInfo> GetWeatherAsync(WeatherProviderContext context, CancellationToken cancellationToken = default);
}

public sealed record WeatherProviderContext(
    double Latitude,
    double Longitude,
    string CityId,
    string ApiKey,
    bool UseHttp);
