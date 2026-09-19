using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClassIsland.Core.Models.Weather;

namespace ClassIsland.Services.WeatherProviders;

public sealed class OpenWeatherProvider : IWeatherProvider
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public string Id => "openweather";

    public async Task<WeatherInfo> GetWeatherAsync(WeatherProviderContext context, CancellationToken cancellationToken = default)
    {
        var apiKey = context.ApiKey?.Trim() ?? string.Empty;
        if (apiKey.Length == 0)
        {
            throw new InvalidOperationException("OpenWeather API Key 未配置，请在天气设置中填写后重试。");
        }

        var latitude = context.Latitude.ToString(CultureInfo.InvariantCulture);
        var longitude = context.Longitude.ToString(CultureInfo.InvariantCulture);
        var escapedKey = Uri.EscapeDataString(apiKey);
        var currentUri = $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&units=metric&lang=zh_cn&appid={escapedKey}";
        var forecastUri = $"https://api.openweathermap.org/data/2.5/forecast?lat={latitude}&lon={longitude}&units=metric&lang=zh_cn&appid={escapedKey}";

        try
        {
            using var currentDocument = await GetDocumentAsync(currentUri, cancellationToken);

            try
            {
                using var forecastDocument = await GetDocumentAsync(forecastUri, cancellationToken);
                return Map(currentDocument.RootElement, forecastDocument.RootElement);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                using var emptyForecastDocument = JsonDocument.Parse("{\"list\":[]}");
                return Map(currentDocument.RootElement, emptyForecastDocument.RootElement);
            }
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("OpenWeather 请求超时，请检查网络后重试。");
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("无法连接 OpenWeather 服务，请检查网络后重试。", exception);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("OpenWeather 返回的数据格式无效，请稍后重试。", exception);
        }
    }

    private static async Task<JsonDocument> GetDocumentAsync(string uri, CancellationToken cancellationToken)
    {
        using var response = await HttpClient.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateStatusException(response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static InvalidOperationException CreateStatusException(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => new InvalidOperationException("OpenWeather 请求参数无效，请检查天气位置坐标。"),
        HttpStatusCode.Unauthorized => new InvalidOperationException("OpenWeather API Key 无效，请检查后重试。"),
        HttpStatusCode.Forbidden => new InvalidOperationException("当前 OpenWeather API Key 无权访问该接口。"),
        HttpStatusCode.NotFound => new InvalidOperationException("OpenWeather 接口不可用，请稍后重试。"),
        HttpStatusCode.TooManyRequests => new InvalidOperationException("OpenWeather 请求受限，可能已达到调用频率或配额上限。"),
        >= HttpStatusCode.InternalServerError => new InvalidOperationException("OpenWeather 服务暂时不可用，请稍后重试。"),
        _ => new InvalidOperationException($"OpenWeather 请求失败，HTTP 状态码 {(int)statusCode}。")
    };

    private static WeatherInfo Map(JsonElement current, JsonElement forecast)
    {
        var currentMain = current.TryGetProperty("main", out var main) ? main : default;
        var currentWind = current.TryGetProperty("wind", out var wind) ? wind : default;
        var forecastItems = forecast.TryGetProperty("list", out var list)
            ? list.EnumerateArray().ToList()
            : new List<JsonElement>();
        var hourlyItems = forecastItems.Take(8).ToList();
        var dailyGroups = forecastItems
            .Where(item => item.TryGetProperty("dt", out _))
            .GroupBy(item => DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("dt").GetInt64()).LocalDateTime.Date)
            .Take(5)
            .ToList();

        return new WeatherInfo
        {
            UpdateTimeUnix = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Current = new CurrentWeather
            {
                Temperature = Pair(currentMain, "temp", "°C"),
                FeelsLike = Pair(currentMain, "feels_like", "°C"),
                Humidity = Pair(currentMain, "humidity", "%"),
                Pressure = Pair(currentMain, "pressure", "hPa"),
                Visibility = Pair(current, "visibility", "m"),
                Weather = MapWeatherCode(GetWeatherId(current)),
                PublishTime = GetUnixDateTime(current, "dt", DateTime.Now),
                Wind = new WindInfo
                {
                    Speed = Pair(currentWind, "speed", "m/s"),
                    Direction = Pair(currentWind, "deg", "°")
                }
            },
            ForecastDaily = new ForecastDaily
            {
                Temperature = new StatusValueBase<List<RangedValue>>
                {
                    Value = dailyGroups.Select(group => new RangedValue
                    {
                        From = group.Select(item => GetNestedNumber(item, "main", "temp_min")).Where(value => value.HasValue).DefaultIfEmpty().Min()?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty,
                        To = group.Select(item => GetNestedNumber(item, "main", "temp_max")).Where(value => value.HasValue).DefaultIfEmpty().Max()?.ToString("0.#", CultureInfo.InvariantCulture) ?? string.Empty
                    }).ToList()
                },
                Weather = new StatusValueBase<List<RangedValue>>
                {
                    Value = dailyGroups.Select(group =>
                    {
                        var representative = group.OrderBy(item => Math.Abs(GetUnixDateTime(item, "dt", DateTime.MinValue).Hour - 12)).First();
                        var code = MapWeatherCode(GetWeatherId(representative));
                        return new RangedValue { From = code, To = code };
                    }).ToList()
                },
                PrecipitationProbability = new StatusValueBase<List<string>>
                {
                    Value = dailyGroups.Select(group => Math.Round(group.Select(GetPrecipitationProbability).DefaultIfEmpty(0).Max() * 100).ToString(CultureInfo.InvariantCulture)).ToList()
                },
                SunRiseSet = new StatusValueBase<List<RangedValue>>
                {
                    Value = dailyGroups.Select(_ => new RangedValue()).ToList()
                }
            },
            ForecastHourly = new ForecastHourly
            {
                Temperature = new StatusValueBase<List<int>>
                {
                    Value = hourlyItems.Select(item => (int)Math.Round(GetNestedNumber(item, "main", "temp") ?? 0)).ToList()
                },
                Weather = new StatusValueBase<List<int>>
                {
                    Value = hourlyItems.Select(item => int.Parse(MapWeatherCode(GetWeatherId(item)), CultureInfo.InvariantCulture)).ToList()
                }
            }
        };
    }

    private static ValueUnitPair Pair(JsonElement element, string property, string unit) => new()
    {
        Value = element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble().ToString("0.#", CultureInfo.InvariantCulture)
            : string.Empty,
        Unit = unit
    };

    private static double? GetNestedNumber(JsonElement element, string parent, string property) =>
        element.TryGetProperty(parent, out var nested) && nested.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static double GetPrecipitationProbability(JsonElement element) =>
        element.TryGetProperty("pop", out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;

    private static DateTime GetUnixDateTime(JsonElement element, string property, DateTime fallback) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var unixTime)
            ? DateTimeOffset.FromUnixTimeSeconds(unixTime).LocalDateTime
            : fallback;

    private static int GetWeatherId(JsonElement element) =>
        element.TryGetProperty("weather", out var weather) && weather.ValueKind == JsonValueKind.Array && weather.GetArrayLength() > 0 &&
        weather[0].TryGetProperty("id", out var id) && id.TryGetInt32(out var code)
            ? code
            : -1;

    internal static string MapWeatherCode(int code) => code switch
    {
        >= 200 and < 300 => "4",
        >= 300 and < 400 => "7",
        >= 500 and < 600 => code >= 520 ? "9" : "8",
        >= 600 and < 700 => "16",
        701 or 711 or 721 or 741 => "18",
        731 or 751 or 761 or 762 => "20",
        771 or 781 => "29",
        800 => "0",
        801 => "1",
        802 => "2",
        803 or 804 => "3",
        _ => "99"
    };
}

