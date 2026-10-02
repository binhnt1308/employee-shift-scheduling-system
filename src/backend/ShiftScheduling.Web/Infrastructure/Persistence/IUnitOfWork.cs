namespace ShiftScheduling.Infrastructure.Persistence;

// Điểm lưu thay đổi của một nghiệp vụ để Service có thể ghi nhiều entity trong cùng đơn vị công việc.
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
