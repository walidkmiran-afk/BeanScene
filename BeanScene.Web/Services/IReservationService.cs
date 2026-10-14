using BeanScene.Web.Models;
using BeanScene.Web.Models.ViewModels;

namespace BeanScene.Web.Services;

public interface IReservationService
{
    Task<OperationResult<Reservation>> CreateAsync(ReservationInput input, string? memberUserId = null);
    Task<string> DescribeGuestAsync(ReservationInput input, string? memberUserId = null);
    Task<OperationResult> UpdateAsync(ReservationInput input);
    Task<OperationResult> ChangeStatusAsync(int reservationId, ReservationStatus target);
    Task<OperationResult> AssignTablesAsync(int reservationId, IEnumerable<int> tableIds);
    Task<OperationResult> DeleteAsync(int reservationId);

    Task<Reservation?> GetAsync(int reservationId);
    Task<List<Reservation>> SearchAsync(ReservationSearchCriteria criteria);
    Task<List<Reservation>> GetPendingAsync();
    Task<List<Reservation>> GetForMemberAsync(string userId);
    Task<int> GuestsBookedAsync(int sittingId, int? excludeReservationId = null);
    Task<HashSet<int>> GetUnavailableTableIdsAsync(int reservationId);
}
