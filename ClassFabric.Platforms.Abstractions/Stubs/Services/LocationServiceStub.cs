using ClassFabric.Platforms.Abstraction.Models;
using ClassFabric.Platforms.Abstraction.Services;

namespace ClassFabric.Platforms.Abstraction.Stubs.Services;

/// <summary>
/// 位置服务桩
/// </summary>
public class LocationServiceStub : ILocationService
{
    internal LocationServiceStub()
    {
        
    }
    
    public async Task<LocationCoordinate> GetLocationAsync()
    {
        return new LocationCoordinate()
        {
            Longitude = 0,
            Latitude = 0
        };
    }
}