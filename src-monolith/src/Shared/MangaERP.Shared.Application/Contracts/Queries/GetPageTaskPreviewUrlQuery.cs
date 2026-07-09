using MediatR;
using System;

namespace MangaERP.Shared.Application.Contracts.Queries;

public record GetPageTaskPreviewUrlQuery(Guid PageId) : IRequest<string?>;
