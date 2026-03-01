using System.Net.Http.Json;
using System.Text.Json;

using Microsoft.Extensions.Logging;

using RatingService.Domain;

namespace RatingService.Infrastructure;

public class ReservationServiceClient(HttpClient http, ILogger<ReservationServiceClient> logger)
    : IReservationServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<bool> HasCompletedStayAsync(Guid guestId, Guid targetId, string targetType)
    {
        try
        {
            var url = $"/api/reservations/internal/completed?guestId={guestId}&targetId={targetId}&targetType={targetType}";
            var response = await http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CompletedStayResponse>(JsonOptions);
            return result?.HasCompleted ?? false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check completed stay for guest {GuestId} target {TargetId}", guestId, targetId);
            // Fail closed: deny rating if we can't verify
            return false;
        }
    }

    private class CompletedStayResponse
    {
        public bool HasCompleted { get; set; }
    }
}
