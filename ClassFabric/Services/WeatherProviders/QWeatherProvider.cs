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

/// <summary>
/// 和风天气（QWeather）数据源。免费开发版每天约 1000 次请求、需自行申请 Key；
/// 部分用户使用自建/代理 Host，因此 ApiHost 可配置，留空走官方默认域名。
/// </summary>
public sealed class QWeatherProvider : IWeatherProvider
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public string Id => "qweather";

    public async Task<WeatherInfo> GetWeatherAsync(WeatherProviderContext context, CancellationToken cancellationToken = default)
    {
        var apiKey = context.ApiKey?.Trim() ?? string.Empty;
        if (apiKey.Length == 0)
        {
            throw new InvalidOperationException("QWeather API Key 未配置，请在天气设置中填写后重试。");
        }

        var host = string.IsNullOrWhiteSpace(context.ApiHost) ? "devapi.qweather.com" : context.ApiHost.Trim().TrimEnd('/');
        var geoHost = host.StartsWith("devapi.", StringComparison.OrdinalIgnoreCase)
            ? "geoapi" + host["devapi".Length..]
            : "geoapi.qweather.com";

        // 和风天气要求先拿 LocationID（城市 ID）再查天气，经纬度通过 GeoAPI 换算。
        var locationId = context.CityId;
        if (string.IsNullOrWhiteSpace(locationId) || !locationId.StartsWith("10", StringComparison.Ordinal))
        {
            var lookupUri = $"https://{geoHost}/v2/city/lookup?location={FormatNumber(context.Latitude)},{FormatNumber(context.Longitude)}&number=1";
            using var lookupDocument = await GetDocumentAsync(lookupUri, apiKey, cancellationToken);
            locationId = ReadFirstCode(lookupDocument.RootElement, "location");
        }
        if (string.IsNullOrWhiteSpace(locationId))
        {
            throw new InvalidOperationException("无法通过和风天气 GeoAPI 定位城市，请检查天气位置设置。");
        }

        var currentUri = $"https://{host}/v7/weather/now?location={locationId}";
        var dailyUri = $"https://{host}/v7/weather/7d?location={locationId}";
        var hourlyUri = $"https://{host}/v7/weather/24h?location={locationId}";
        var alertUri = $"https://{host}/v7/warning/now?location={locationId}";

        using var currentDocument = await GetDocumentAsync(currentUri, apiKey, cancellationToken);
        // 预报与预警任一失败不整体失败：先保实时数据可用（预警拉不到仅作降级）。
        string? dailyJson = null;
        string? hourlyJson = null;
        string? alertJson = null;
        try
        {
            dailyJson = await GetStringAsync(dailyUri, apiKey, cancellationToken);
            hourlyJson = await GetStringAsync(hourlyUri, apiKey, cancellationToken);
            alertJson = await GetStringAsync(alertUri, apiKey, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            // 降级：保留已拿到的部分，其余用空结构填充。
        }

        var info = MapCurrent(currentDocument.RootElement);
        if (dailyJson != null)
        {
            using var dailyDocument = JsonDocument.Parse(dailyJson);
            MapDaily(dailyDocument.RootElement, info);
        }
        if (hourlyJson != null)
        {
            using var hourlyDocument = JsonDocument.Parse(hourlyJson);
            MapHourly(hourlyDocument.RootElement, info);
        }
        if (alertJson != null)
        {
            using var alertDocument = JsonDocument.Parse(alertJson);
            MapAlerts(alertDocument.RootElement, info);
        }
        return info;
    }

    private static WeatherInfo MapCurrent(JsonElement current)
    {
        var now = current.GetProperty("now");
        var obsTime = ReadString(now, "obsTime");
        return new WeatherInfo
        {
            UpdateTimeUnix = DateTimeOffset.TryParse(obsTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var obs)
                ? obs.ToUnixTimeMilliseconds()
                : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Current = new CurrentWeather
            {
                Temperature = new ValueUnitPair
                {
                    Value = ReadString(now, "temp"),
                    Unit = "°C"
                },
                FeelsLike = new ValueUnitPair
                {
                    Value = ReadString(now, "feelsLike"),
                    Unit = "°C"
                },
                Humidity = new ValueUnitPair
                {
                    Value = ReadString(now, "humidity"),
                    Unit = "%"
                },
                Pressure = new ValueUnitPair
                {
                    Value = ReadString(now, "pressure"),
                    Unit = "hPa"
                },
                Visibility = new ValueUnitPair
                {
                    Value = ReadString(now, "vis"),
                    Unit = "km"
                },
                Weather = MapWeatherCode(ReadString(now, "icon")),
                PublishTime = DateTime.Now,
                Wind = new WindInfo
                {
                    Speed = new ValueUnitPair
                    {
                        Value = ReadString(now, "windSpeed"),
                        Unit = "km/h"
                    },
                    Direction = new ValueUnitPair
                    {
                        Value = ReadString(now, "wind360"),
                        Unit = "°"
                    }
                }
            }
        };
    }

    private static void MapDaily(JsonElement root, WeatherInfo info)
    {
        if (!root.TryGetProperty("daily", out var daily) || daily.GetArrayLength() == 0)
        {
            return;
        }

        info.ForecastDaily.Temperature = new StatusValueBase<List<RangedValue>>
        {
            Value = daily.EnumerateArray().Select(day => new RangedValue
            {
                From = ReadString(day, "tempMin"),
                To = ReadString(day, "tempMax")
            }).ToList()
        };
        info.ForecastDaily.Weather = new StatusValueBase<List<RangedValue>>
        {
            Value = daily.EnumerateArray().Select(day =>
            {
                var code = MapWeatherCode(ReadString(day, "iconDay"));
                return new RangedValue { From = code, To = code };
            }).ToList()
        };
        info.ForecastDaily.PrecipitationProbability = new StatusValueBase<List<string>>
        {
            // 和风逐日不直接给降水概率，给出降水量级文本；数值统一按 0 处理避免误导。
            Value = daily.EnumerateArray().Select(_ => "0").ToList()
        };
        info.ForecastDaily.SunRiseSet = new StatusValueBase<List<RangedValue>>
        {
            Value = daily.EnumerateArray().Select(day => new RangedValue
            {
                From = ReadString(day, "sunrise"),
                To = ReadString(day, "sunset")
            }).ToList()
        };
    }

    private static void MapHourly(JsonElement root, WeatherInfo info)
    {
        if (!root.TryGetProperty("hourly", out var hourly))
        {
            return;
        }

        info.ForecastHourly.Temperature = new StatusValueBase<List<int>>
        {
            Value = hourly.EnumerateArray().Select(hour =>
                int.TryParse(ReadString(hour, "temp"), out var t) ? t : 0).ToList()
        };
        info.ForecastHourly.Weather = new StatusValueBase<List<int>>
        {
            Value = hourly.EnumerateArray().Select(hour =>
                int.TryParse(MapWeatherCode(ReadString(hour, "icon")), CultureInfo.InvariantCulture, out var c) ? c : 99).ToList()
        };
    }

    private static void MapAlerts(JsonElement root, WeatherInfo info)
    {
        if (!root.TryGetProperty("warning", out var warnings))
        {
            return;
        }

        info.Alerts = warnings.EnumerateArray().Select(warning => new WeatherAlert
        {
            LocationKey = ReadString(warning, "location"),
            AlertId = ReadString(warning, "id"),
            PubTime = DateTimeOffset.TryParse(ReadString(warning, "pubTime"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var pub)
                ? pub.LocalDateTime
                : DateTime.Now,
            Title = ReadString(warning, "title"),
            Type = ReadString(warning, "type"),
            Level = ReadString(warning, "level"),
            Detail = ReadString(warning, "text"),
            Images = string.IsNullOrWhiteSpace(ReadString(warning, "icon"))
                ? new Dictionary<string, string>()
                : new Dictionary<string, string> { ["icon"] = ReadString(warning, "icon")! }
        }).ToList();
    }

    private static async Task<JsonDocument> GetDocumentAsync(string uri, string apiKey, CancellationToken cancellationToken)
    {
        var json = await GetStringAsync(uri, apiKey, cancellationToken);
        return JsonDocument.Parse(json);
    }

    private static async Task<string> GetStringAsync(string uri, string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("X-QW-Api-Key", apiKey);
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw CreateStatusException(response.StatusCode);
        }
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static InvalidOperationException CreateStatusException(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new InvalidOperationException("QWeather API Key 无效或无权访问该接口，请检查后重试。"),
        HttpStatusCode.NotFound => new InvalidOperationException("QWeather 接口不可用，请检查 API Host 设置。"),
        HttpStatusCode.TooManyRequests => new InvalidOperationException("QWeather 请求受限，可能已达到调用频率或配额上限。"),
        >= HttpStatusCode.InternalServerError => new InvalidOperationException("QWeather 服务暂时不可用，请稍后重试。"),
        _ => new InvalidOperationException($"QWeather 请求失败，HTTP 状态码 {(int)statusCode}。")
    };

    private static string FormatNumber(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string ReadString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string ReadFirstCode(JsonElement root, string arrayProperty) =>
        root.TryGetProperty(arrayProperty, out var array) && array.GetArrayLength() > 0 &&
        array[0].TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// 和风 v7 现行天气码（100/300/400/500/900 系，官方文档 dev.qweather.com/docs/api/weather/weather-conditions）
    /// 折算到小米天气码表（Assets/XiaomiWeather/xiaomi_weather_status.json，全应用统一用小米码渲染图标）。
    /// 折算取「同现象、量级就近」：和风分级比小米细，映射时小雨-中雨这类区间码取小米相邻量级。
    /// </summary>
    internal static string MapWeatherCode(string icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return "99";
        }

        return icon.Trim() switch
        {
            "100" or "150" => "0",                       // 晴 / 晴（夜）
            "101" or "151" => "1",                       // 多云 / 多云（夜）
            "102" or "103" or "152" or "153" => "1",     // 少云、晴间多云 → 多云（小米无此细分）
            "104" => "2",                                   // 阴
            "300" or "301" => "3",                         // 阵雨、强阵雨
            "302" or "303" => "4",                         // 雷阵雨、强雷阵雨
            "304" => "5",                                   // 雷阵雨伴有冰雹
            "305" or "309" => "7",                         // 小雨、毛毛雨/细雨
            "306" => "8",                                   // 中雨
            "307" => "9",                                   // 大雨
            "308" or "310" => "10",                        // 极端降雨、暴雨
            "311" => "11",                                  // 大暴雨
            "312" => "12",                                  // 特大暴雨
            "313" => "19",                                  // 冻雨
            "314" => "21",                                  // 小到中雨
            "315" => "22",                                  // 中到大雨
            "316" => "23",                                  // 大到暴雨
            "317" => "24",                                  // 暴雨到大暴雨
            "318" => "25",                                  // 大暴雨到特大暴雨
            "399" => "21",                                  // 雨（综合码，取小雨-中雨）
            "400" => "14",                                  // 小雪
            "401" => "15",                                  // 中雪
            "402" or "403" => "16",                        // 大雪、暴雪
            "404" => "6",                                   // 雨夹雪
            "405" => "6",                                   // 雨雪天气 → 雨夹雪
            "406" => "3",                                   // 阵雨夹雪 → 阵雨
            "407" => "13",                                  // 阵雪
            "408" => "26",                                  // 小到中雪
            "409" => "27",                                  // 中到大雪
            "410" => "28",                                  // 大到暴雪
            "499" => "26",                                  // 雪（综合码）
            "500" or "501" or "509" or "510" or "514" or "515" => "18",  // 各级雾
            "502" or "511" or "512" or "513" => "53",     // 各级霾
            "503" or "504" => "29",                        // 扬沙、浮尘
            "507" => "20",                                  // 沙尘暴
            "508" => "31",                                  // 强沙尘暴
            "900" or "901" => "99",                        // 热、冷（小米表无对应）
            "999" => "99",                                  // 未知
            _ => "99"
        };
    }
}
