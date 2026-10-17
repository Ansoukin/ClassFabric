using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Platform;
using ClassIsland.Core.Extensions;
using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Models.Ruleset;
using ClassIsland.Core.Models.Weather;
using ClassIsland.Helpers;
using ClassIsland.Models;
using ClassIsland.Models.Rules;
using ClassIsland.Platforms.Abstraction.Services;
using ClassIsland.Services.WeatherProviders;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ClassIsland.Services;

public class WeatherService : ObservableRecipient, IHostedService, IWeatherService
{
    private IDataTemplate? _selectedWeatherIconTemplate;
    private SettingsService SettingsService { get; }

    private Settings Settings => SettingsService.Settings;
    private string Schema =>  Settings.NoTLSWeatherRequests ? "http" : "https";
    public List<XiaomiWeatherStatusCodeItem> WeatherStatusList { get; set; } = new();

    private ILogger<WeatherService> Logger { get; }
    public IRulesetService RulesetService { get; }
    public ILocationService LocationService { get; }
    private IReadOnlyDictionary<string, IWeatherProvider> WeatherProviders { get; }

    private DispatcherTimer UpdateTimer { get; } = new()
    {
        Interval = TimeSpan.FromMinutes(5)
    };

    public bool IsWeatherRefreshed { get; set; } = false;

    public bool IsPosUpdated { get; set; } = false;

    public WeatherService(SettingsService settingsService, ILogger<WeatherService> logger, IRulesetService rulesetService, ILocationService locationService, IEnumerable<IWeatherProvider> weatherProviders)
    {
        Logger = logger;
        RulesetService = rulesetService;
        LocationService = locationService;
        WeatherProviders = weatherProviders.ToDictionary(provider => provider.Id, StringComparer.OrdinalIgnoreCase);
        SettingsService = settingsService;
        SettingsService.Settings.PropertyChanged += SettingsOnPropertyChanged;
        LoadData();
        LoadWeatherIconTemplate();
        RulesetService.RegisterRuleHandler("classfabric.weather.currentWeather", CurrentWeatherRuleHandler);
        RulesetService.RegisterRuleHandler("classfabric.weather.tomorrowWeather", TomorrowWeatherRuleHandler);
        RulesetService.RegisterRuleHandler("classfabric.weather.hasWeatherAlert", HasAlertRuleHandler);
        RulesetService.RegisterRuleHandler("classfabric.weather.rainTime", RainTimeRuleHandler);
        RulesetService.RegisterRuleHandler("classfabric.weather.sunRiseSet", SunRiseSetRuleHandler);
        UpdateTimer.Tick += UpdateTimerOnTick;
        UpdateTimer.Start();
        _ = QueryWeatherAsync();
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(Settings.WeatherIconId))
        {
            return;
        }

        LoadWeatherIconTemplate();
    }

    private void LoadWeatherIconTemplate()
    {
        SelectedWeatherIconTemplate = IWeatherService.RegisteredTemplates.FirstOrDefault(x => x.Id == Settings.WeatherIconId)?.Template;
        if (SelectedWeatherIconTemplate == null)
        {
            Logger.LogWarning("未找到 ID 为 {} 的天气图标模板", Settings.WeatherIconId);
        }
    }

    private bool RainTimeRuleHandler(object? o)
    {
        if (o is not RainTimeRuleSettings settings)
        {
            return false;
        }

        var baseTime = (settings.IsRemainingTime ? -1.0 : 1.0) * Settings.LastWeatherInfo.Minutely.Precipitation.RainRemainingMinutes;
        return baseTime > 0 && baseTime <= settings.RainTimeMinutes;
        
    }

    private bool SunRiseSetRuleHandler(object? o)
    {
        if (o is not SunRiseSetRuleSettings settings)
            return false;
        if (!IsWeatherRefreshed)
            return false;

        var now = DateTimeOffset.Now;
        if (!TryGetSunTimes(now, out var sunrise, out var sunset))
            return false;

        return settings.IsSunset
            ? (now >= sunset || now < sunrise)
            : (now >= sunrise && now < sunset);
    }

    private bool TryGetSunTimes(DateTimeOffset now, out DateTimeOffset sunrise, out DateTimeOffset sunset)
    {
        sunrise = default;
        sunset = default;
        var list = Settings.LastWeatherInfo.ForecastDaily.SunRiseSet.Value;
        foreach (var item in list)
        {
            if (!DateTimeOffset.TryParse(item.From, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sr))
                continue;
            if (!DateTimeOffset.TryParse(item.To, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ss))
                continue;
            if (sr.Date == now.Date || ss.Date == now.Date)
            {
                sunrise = sr;
                sunset = ss;
                return true;
            }
        }
        return false;
    }

    private bool HasAlertRuleHandler(object? o)
    {
        if (o is not StringMatchingSettings settings)
        {
            return false;
        }

        return IsWeatherRefreshed &&
               Settings.LastWeatherInfo.Alerts.Exists(x => settings.IsMatching(x.Title));
    }

    private bool CurrentWeatherRuleHandler(object? o)
    {
        if (o is not CurrentWeatherRuleSettings settings)
        {
            return false;
        }

        return IsWeatherRefreshed &&
               (settings.IsFuzzyMatch 
                   ? IsWeatherCodeMatched(Settings.LastWeatherInfo.Current.Weather, settings.WeatherId.ToString())
                   : settings.WeatherId.ToString() == Settings.LastWeatherInfo.Current.Weather);
    }

    private bool TomorrowWeatherRuleHandler(object? o)
    {
        if (o is not CurrentWeatherRuleSettings settings)
        {
            return false;
        }

        if (!IsWeatherRefreshed || Settings.LastWeatherInfo.ForecastDaily.Weather.Value == null || Settings.LastWeatherInfo.ForecastDaily.Weather.Value.Count <= 1)
        {
            return false;
        }

        var tomorrowWeather = Settings.LastWeatherInfo.ForecastDaily.Weather.Value[1];
        var targetWeather = settings.WeatherId.ToString();
        
        if (settings.IsFuzzyMatch)
        {
            return IsWeatherCodeMatched(tomorrowWeather.From, targetWeather) ||
                   IsWeatherCodeMatched(tomorrowWeather.To, targetWeather);
        }
        else
        {
            return tomorrowWeather.From == targetWeather || tomorrowWeather.To == targetWeather;
        }
    }

    private static bool IsWeatherCodeMatched(string actualWeatherCode, string targetWeatherCode)
    {
        if (actualWeatherCode == targetWeatherCode)
        {
            return true;
        }

        var actualCodes = ExpandWeatherCode(actualWeatherCode);
        var targetCodes = ExpandWeatherCode(targetWeatherCode);

        return actualCodes.Overlaps(targetCodes);
    }

    private static HashSet<string> ExpandWeatherCode(string weatherCode)
    {
        return weatherCode switch
        {
            "21" => ["7", "8", "21"],
            "22" => ["8", "9", "22"],
            "23" => ["9", "10", "23"],
            "24" => ["10", "11", "24"],
            "25" => ["11", "12", "25"],
            "26" => ["14", "15", "26"],
            "27" => ["15", "16", "27"],
            "28" => ["16", "17", "28"],
            "301" => ["3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "19", "21", "22", "23", "24", "25", "301"],
            "302" => ["6", "13", "14", "15", "16", "17", "26", "27", "28", "34", "302"],
            _ => [weatherCode]
        };
    }

    private async void UpdateTimerOnTick(object? sender, EventArgs e)
    {
        await QueryWeatherAsync();
    }

    /// <summary>
    /// 判断请求数是否已达免费额度上限。仅对带 Key 的数据源生效（小米无额度概念）。
    /// OpenWeather 免费档 1000 次/天；和风天气免费档 50000 次/月。
    /// </summary>
    private bool IsOverUsageLimit(string providerId)
    {
        if (!Settings.WeatherUsageLimitEnabled || providerId is not ("openweather" or "qweather"))
        {
            return false;
        }

        var now = DateTime.Now;
        return providerId switch
        {
            "openweather" => GetCount("openweather", $"{now:yyyy-MM-dd}") >= 1000,
            "qweather" => GetCount("qweather", $"{now:yyyy-MM}") >= 50000,
            _ => false
        };
    }

    private int GetCount(string providerId, string periodKey)
    {
        return Settings.WeatherRequestCounts.TryGetValue($"{providerId}:{periodKey}", out var count) ? count : 0;
    }

    /// <summary>
    /// 成功获取天气后计量。周期键滚出后旧键自然失效，字典只增不减，但每键极小，
    /// 十年体量也不足 1MB，不做清理。
    /// </summary>
    private void CountRequest(string requestedProviderId, string actualProviderId)
    {
        if (requestedProviderId is not ("openweather" or "qweather"))
        {
            return;
        }

        // 按实际 HTTP 请求计（OpenWeather 一轮 current+forecast 共 2 次、和风一轮最多 5 次），
        // 这是服务商标的计费口径；实际提供者可能因达限回退成了小米，此时按原请求源计量。
        var httpCalls = actualProviderId switch
        {
            "openweather" => 2,
            "qweather" => 5,
            _ => 1
        };
        if (actualProviderId != requestedProviderId && actualProviderId == "xiaomi")
        {
            // 原请求源失败回退小米：原源只计 1 次失败请求，不按满轮计。
            httpCalls = 1;
        }

        var now = DateTime.Now;
        var dayKey = $"{requestedProviderId}:{now:yyyy-MM-dd}";
        var monthKey = $"{requestedProviderId}:{now:yyyy-MM}";
        Settings.WeatherRequestCounts[dayKey] = GetCount(requestedProviderId, $"{now:yyyy-MM-dd}") + httpCalls;
        Settings.WeatherRequestCounts[monthKey] = GetCount(requestedProviderId, $"{now:yyyy-MM}") + httpCalls;
        // 直接改字典不会触发 PropertyChanged，但计量表无需通知 UI，保存机制靠 SettingsChanged 兜底。
    }

    private async void LoadData()
    {
var json = await AssetLoader.ReadAllTextAsync(new Uri(
            "avares://ClassFabric/Assets/XiaomiWeather/xiaomi_weather_status.json", UriKind.Absolute));
        var codes = JsonSerializer.Deserialize<XiaomiWeatherStatusCodes>(json);
        WeatherStatusList = codes!.WeatherInfo;
    }

    public async Task QueryWeatherAsync()
    {
        await QueryWeatherWithResultAsync();
    }

    public async Task<WeatherQueryResult> QueryWeatherWithResultAsync()
    {
        var result = new WeatherQueryResult();
        if (!IsPosUpdated && Settings.AutoRefreshWeatherLocation)
        {
            IsPosUpdated = true;
            try
            {
                var pos = await LocationService.GetLocationAsync();
                Settings.WeatherLongitude = Math.Round(pos.Longitude, 4);
                Settings.WeatherLatitude = Math.Round(pos.Latitude, 4);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "无法获取当前位置");
            }
        }

        var cityLatitude = string.Empty;
        var cityLongitude = string.Empty;
        
        // 获取城市信息
        try
        {
            if (Settings.WeatherLocationSource == 1)
            {
                var precisions = new[] { 4, 3, 2, 1, 0 };
                bool found = false;
                int usedPrecision = 4;
                foreach (var precision in precisions)
                {
                    var factor = Math.Pow(10, precision);
                    List<(double lon, double lat)> candidates;
                    if (precision == 0)
                    {
                        candidates = new List<(double, double)>
                        {
                            (Math.Round(Settings.WeatherLongitude, 0), Math.Round(Settings.WeatherLatitude, 0))
                        };
                    }
                    else
                    {
                        candidates = new List<(double, double)>
                        {
                            (Math.Round(Settings.WeatherLongitude, precision), Math.Round(Settings.WeatherLatitude, precision)),
                            (Math.Truncate(Settings.WeatherLongitude * factor) / factor, Math.Truncate(Settings.WeatherLatitude * factor) / factor)
                        };
                    }
                    foreach (var (lon, lat) in candidates)
                    {
                        var uri = $"{Schema}://weatherapi.market.xiaomi.com/wtr-v3/location/city/geo?longitude={lon}&latitude={lat}&locale=zh_cn";
                        Logger.LogInformation("获取城市信息： {}", uri);
                        var cityInfoList = await WebRequestHelper.Default.GetJson<List<CityInfo>>(new Uri(uri));
                        var cityInfo = cityInfoList.FirstOrDefault();
                        Logger.LogDebug("城市信息: Count={Count}, Status={Status}, FirstLocationKey={LocationKey}, FirstName={Name}, FirstAffiliation={Affiliation}, FirstLongitude={Longitude}, FirstLatitude={Latitude}",
                            cityInfoList?.Count ?? 0, cityInfo?.Status, cityInfo?.LocationKey, cityInfo?.Name, cityInfo?.Affiliation, cityInfo?.Longitude,
                            cityInfo?.Latitude);
                        if (cityInfo != null && cityInfo.Status == 0 && !string.IsNullOrWhiteSpace(cityInfo.LocationKey))
                        {
                            cityLatitude = lat.ToString(CultureInfo.InvariantCulture);
                            cityLongitude = lon.ToString(CultureInfo.InvariantCulture);
                            Settings.CityId = cityInfo.LocationKey;
                            Settings.CityName = $"{cityInfo.Name} ({cityInfo.Affiliation})";
                            Logger.LogDebug("城市信息精度: {Precision}", precision);
                            usedPrecision = precision;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }
                if (!found)
                {
                    result.IsSuccess = false;
                    result.ErrorMessage = "无法通过经纬度获取城市信息";
                    throw new InvalidOperationException(result.ErrorMessage);
                }
                if (usedPrecision < 4)
                {
                    result.IsPrecisionDegraded = true;
                    result.DegradedPrecision = usedPrecision;
                }
            }
            else
            {
                // CityId
                var uri = $"{Schema}://weatherapi.market.xiaomi.com/wtr-v3/location/city/info?locationKey={Settings.CityId}&locale=zh_cn";
                Logger.LogInformation("获取城市信息： {}", uri);
                var cityInfoList = await WebRequestHelper.Default.GetJson<List<CityInfo>>(new Uri(uri));
                var cityInfo = cityInfoList.FirstOrDefault();
                Logger.LogTrace("城市信息: Count={Count}, FirstLocationKey={LocationKey}, FirstName={Name}, FirstAffiliation={Affiliation}",
                    cityInfoList?.Count ?? 0, cityInfo?.LocationKey, cityInfo?.Name, cityInfo?.Affiliation);
                if (cityInfo != null && cityInfo.LocationKey == Settings.CityId && !string.IsNullOrWhiteSpace(cityInfo.LocationKey))
                {
                    cityLatitude = cityInfo.Latitude;
                    cityLongitude = cityInfo.Longitude;
                }
                else
                {
                    Logger.LogError("无法通过CityId获取城市信息");
                    result.ErrorMessage = "无法通过CityId获取城市信息";
                    return result;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取城市信息失败。");
            result.ErrorMessage = ex.Message;
            return result;
        }

        // 请求天气信息
        try
        {
            Logger.LogTrace("天气请求信息: CityId={CityId}, Longitude={Longitude}, Latitude={Latitude}, LocationSource={LocationSource}",
                Settings.CityId, cityLongitude, cityLatitude, Settings.WeatherLocationSource);
            var latitude = double.Parse(cityLatitude, CultureInfo.InvariantCulture);
            var longitude = double.Parse(cityLongitude, CultureInfo.InvariantCulture);
            var requestedProviderId = string.IsNullOrWhiteSpace(Settings.WeatherProvider)
                ? "xiaomi"
                : Settings.WeatherProvider;
            // 免费额度保护：计量键按数据源+周期分离，达限后本次刷新改走小米源，周期自然滚动后恢复。
            // 小米源本身无 Key 无额度，不参与计量；手动刷新同样受限，避免用户狂点把仅剩额度打光。
            var providerId = IsOverUsageLimit(requestedProviderId) ? "xiaomi" : requestedProviderId;
            if (providerId != requestedProviderId)
            {
                result.ErrorMessage = "已达到天气服务免费额度上限，本次已改用小米天气，额度周期重置后自动恢复。";
            }
            var context = new WeatherProviderContext(latitude, longitude, Settings.CityId,
                requestedProviderId == "openweather" ? Settings.OpenWeatherApiKey :
                requestedProviderId == "qweather" ? Settings.QWeatherApiKey : "",
                Settings.NoTLSWeatherRequests, Settings.QWeatherApiHost);
            if (!WeatherProviders.TryGetValue(providerId, out var provider))
            {
                Logger.LogWarning("未知天气源 {ProviderId}，已回退到小米天气。", providerId);
                provider = WeatherProviders["xiaomi"];
            }

            WeatherInfo info;
            try
            {
                info = await provider.GetWeatherAsync(context);
            }
            catch (Exception providerException) when (provider.Id != "xiaomi" && Settings.WeatherProviderFallbackEnabled)
            {
                Logger.LogWarning(providerException, "天气源 {ProviderId} 获取失败，回退到小米天气。", provider.Id);
                info = await WeatherProviders["xiaomi"].GetWeatherAsync(context);
                result.ErrorMessage = $"{provider.Id} 获��取失败，已回退到小米天气：{providerException.Message}";
            }

            // 数据有效性闸门：天气源异常时会返回 HTTP 200 + 空 JSON（如小米接口的 {}），
            // 反序列化成全默认对象且不抛异常，上面的重试与回退都拦不住；校验不过就保留旧数据，
            // 不覆盖 LastWeatherInfo，等下个刷新周期重试（上游问题 ClassIsland/ClassIsland#2031）。
            if (info.UpdateTimeUnix == 0 ||
                (string.IsNullOrWhiteSpace(info.Current.Temperature.Value) && info.Current.Weather == "99"))
            {
                Logger.LogWarning(
                    "天气源返回数据无效（updateTime={UpdateTime}, temperature={Temperature}, weather={Weather}），保留上次数据。",
                    info.UpdateTimeUnix, info.Current.Temperature.Value, info.Current.Weather);
                result.IsSuccess = false;
                result.ErrorMessage = "天气接口返回数据异常，已保留上次数据，将在下个刷新周期重试。";
                return result;
            }

            // 排除天气预警
            var validExclusions = Settings.ExcludedWeatherAlerts
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            if (validExclusions.Length > 0)
            {
                info.Alerts.RemoveAll(alert =>
                    validExclusions.Any(exclusion =>
                        alert.Title.Contains(exclusion) || alert.Detail.Contains(exclusion)
                    ));
            }

            // 去重天气预警
            var latest = info.Alerts
                .GroupBy(a => a.Type)
                .ToDictionary(g => g.Key, g => g.MaxBy(a => a.PubTime)!);
            foreach (var a in info.Alerts)
            {
                if (latest[a.Type].AlertId != a.AlertId)
                    Logger.LogInformation("已丢弃旧预警：（{}，{}）{}", a.Title, a.PubTime, a.Detail);
            }
            info.Alerts = latest.Values.ToList();

            if (OperatingSystem.IsAndroid())
            {
                foreach (var alert in info.Alerts)
                {
                    if (alert.Images.TryGetValue("icon", out var icon) &&
                        icon.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        alert.Images["icon"] = "https://" + icon[7..];
                    }
                }
            }

            Settings.LastWeatherInfo = info;
            IsWeatherRefreshed = true;
            CountRequest(providerId, provider.Id);
            result.IsSuccess = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "获取天气信息失败。");
            result.IsSuccess = false;
            result.ErrorMessage = ex.Message;
        }
        finally
        {
            RulesetService.NotifyStatusChanged();
        }

        return result;
    }

    public string GetWeatherTextByCode(string code)
    {
        var c = (from i in WeatherStatusList
                where i.Code.ToString() == code
                select i.Weather)
            .ToList();
        return c.Count > 0 ? c[0] : "未知";
    }

    public async Task<List<City>> GetCitiesByName(string name)
    {
        var uri = new Uri($"{Schema}://weatherapi.market.xiaomi.com/wtr-v3/location/city/hots?locale=zh_cn");
        var logText = "获取热门城市信息";

        if (name != string.Empty )
        {
            uri = new Uri(
                $"{Schema}://weatherapi.market.xiaomi.com/wtr-v3/location/city/search?name={Uri.EscapeDataString(name)}&locale=zh_cn");
            logText = logText.Replace("热门", "");
        }

        try
        {
            Logger.LogInformation("{}： {}", logText, uri);

            var cityInfoList = await WebRequestHelper.Default.GetJson<List<CityInfo>>(uri);
            
            var cities = cityInfoList?.Select(cityInfo => new City
            {
                Name = $"{cityInfo.Name} ({cityInfo.Affiliation})",
                CityId = cityInfo.LocationKey
            }).ToList() ?? new List<City>();

            return cities;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{}失败。", logText);
            return [];
        }
    }

    public IDataTemplate? SelectedWeatherIconTemplate
    {
        get => _selectedWeatherIconTemplate;
        private set => SetProperty(ref _selectedWeatherIconTemplate, value);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }
}
