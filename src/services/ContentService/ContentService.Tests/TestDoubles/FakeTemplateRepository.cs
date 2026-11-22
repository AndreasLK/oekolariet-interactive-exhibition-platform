using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;

namespace ContentService.Tests.TestDoubles;

/// <summary>
/// Minimal in-memory <see cref="ITemplateRepository"/> test double.
/// </summary>
internal sealed class FakeTemplateRepository : ITemplateRepository
{
        private readonly Dictionary<Guid, TemplateEntity> _templates = [];

        public void Seed(TemplateEntity template) => this._templates[template.Id] = template;

        public Task<TemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
                Task.FromResult(result: this._templates.TryGetValue(key: id, value: out TemplateEntity? template) ? template : null);

        public Task<bool> ExistsByNameAsync(string name, Guid? excludeTemplateId, CancellationToken cancellationToken) =>
                Task.FromResult(result: false);

        public Task<PagedResult<TemplateEntity>> ListAsync(
                int page,
                int pageSize,
                TemplateComponentType? componentType,
                string? searchTerm,
                CancellationToken cancellationToken) =>
                Task.FromResult(result: new PagedResult<TemplateEntity>
                {
                        Items = this._templates.Values.ToList(),
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = this._templates.Count
                });

        public Task AddAsync(TemplateEntity template, CancellationToken cancellationToken)
        {
                this._templates[template.Id] = template;
                return Task.CompletedTask;
        }

        public Task UpdateAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                Task.CompletedTask;

        public Task DeleteAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                Task.CompletedTask;
}

