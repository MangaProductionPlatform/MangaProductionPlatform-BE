using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MediatR;

namespace MangaERP.Task.Application.Queries.GetAssistantIncome;

public record GetAssistantIncomeQuery(Guid AssistantId) : IRequest<AssistantIncomeDto>;

public record AssistantIncomeDto(
    int TotalFinishedTasks,
    decimal EstimatedIncome,
    string Currency,
    decimal RatePerTask,
    int DeadlineWarningCount,
    bool IsPenalized,
    decimal PenaltyDeductionAmount);

public class GetAssistantIncomeHandler : IRequestHandler<GetAssistantIncomeQuery, AssistantIncomeDto>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IUserRepository _userRepo;
    private const decimal DefaultRatePerTask = 500000; // 500,000 VND per task

    public GetAssistantIncomeHandler(IPageTaskRepository pageTaskRepo, IUserRepository userRepo)
    {
        _pageTaskRepo = pageTaskRepo;
        _userRepo = userRepo;
    }

    public async Task<AssistantIncomeDto> Handle(GetAssistantIncomeQuery query, CancellationToken ct)
    {
        var assistant = await _userRepo.GetByIdAsync(query.AssistantId, ct)
            ?? throw new KeyNotFoundException($"Assistant {query.AssistantId} not found.");

        var tasks = await _pageTaskRepo.GetByAssistantAsync(query.AssistantId, ct);
        var finishedTasksCount = tasks.Count(t => t.TaskStatus == PageTaskStatus.Approved);
        
        var isPenalized = assistant.DeadlineWarningCount >= 3;
        var rate = DefaultRatePerTask;
        if (isPenalized)
        {
            rate = DefaultRatePerTask * 0.8m; // 20% deduction
        }

        var estimatedIncome = finishedTasksCount * rate;
        var penaltyDeductionAmount = finishedTasksCount * (DefaultRatePerTask - rate);

        return new AssistantIncomeDto(
            finishedTasksCount,
            estimatedIncome,
            "VND",
            rate,
            assistant.DeadlineWarningCount,
            isPenalized,
            penaltyDeductionAmount);
    }
}
