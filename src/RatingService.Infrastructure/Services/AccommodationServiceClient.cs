using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using RatingService.Domain;

namespace RatingService.Infrastructure;

public class AccommodationServiceClient(HttpClient http, ILogger<AccommodationServiceClient> logger)
    : IAccommodationServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<AccommodationBasicInfo?> GetAccommodationAsync(Guid accommodationId)
    {
        try
        {
            var response = await http.GetAsync($"/api/accommodation/{accommodationId}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<AccommodationBasicInfo>(JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch accommodation {AccommodationId}", accommodationId);
            return null;
        }
    }
}
