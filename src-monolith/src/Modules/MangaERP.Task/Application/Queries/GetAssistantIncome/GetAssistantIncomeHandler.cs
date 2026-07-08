using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MediatR;

namespace MangaERP.Task.Application.Queries.GetAssistantIncome;

public record GetAssistantIncomeQuery(Guid AssistantId) : IRequest<AssistantIncomeDto>;

public record AssistantIncomeDto(
    int TotalFinishedTasks,
    decimal EstimatedIncome,
    string Currency,
    decimal RatePerTask);

public class GetAssistantIncomeHandler : IRequestHandler<GetAssistantIncomeQuery, AssistantIncomeDto>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private const decimal DefaultRatePerTask = 500000; // 500,000 VND per task

    public GetAssistantIncomeHandler(IPageTaskRepository pageTaskRepo)
    {
        _pageTaskRepo = pageTaskRepo;
    }

    public async Task<AssistantIncomeDto> Handle(GetAssistantIncomeQuery query, CancellationToken ct)
    {
        var tasks = await _pageTaskRepo.GetByAssistantAsync(query.AssistantId, ct);
        var finishedTasksCount = tasks.Count(t => t.TaskStatus == PageTaskStatus.Approved);
        
        var estimatedIncome = finishedTasksCount * DefaultRatePerTask;

        return new AssistantIncomeDto(
            finishedTasksCount,
            estimatedIncome,
            "VND",
            DefaultRatePerTask);
    }
}
