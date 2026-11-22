using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;

namespace ContentService.Tests.Api.Fakes;

/// <summary>
/// In-memory <see cref="ITemplateRepository"/> test double for API pipeline tests.
/// </summary>
internal sealed class InMemoryTemplateRepository : ITemplateRepository
{
        private readonly InMemoryContentState _state;

        public InMemoryTemplateRepository(InMemoryContentState state) => this._state = state;

        public Task<TemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
                Task.FromResult(result: this._state.Templates.TryGetValue(key: id, value: out TemplateEntity? template) ? template : null);

        public Task<bool> ExistsByNameAsync(string name, Guid? excludeTemplateId, CancellationToken cancellationToken)
        {
                string normalized = name.Trim();
                bool exists = this._state.Templates.Values.Any(template =>
                        string.Equals(a: template.Name, b: normalized, comparisonType: StringComparison.OrdinalIgnoreCase)
                        && (!excludeTemplateId.HasValue || template.Id != excludeTemplateId.Value));
                return Task.FromResult(result: exists);
        }

        public Task<PagedResult<TemplateEntity>> ListAsync(
                int page,
                int pageSize,
                TemplateComponentType? componentType,
                string? searchTerm,
                CancellationToken cancellationToken)
        {
                IEnumerable<TemplateEntity> query = this._state.Templates.Values;
                if (componentType.HasValue)
                {
                        query = query.Where(template => template.ComponentType == componentType.Value);
                }

                if (!string.IsNullOrWhiteSpace(value: searchTerm))
                {
                        query = query.Where(template => template.Name.Contains(value: searchTerm.Trim(), comparisonType: StringComparison.OrdinalIgnoreCase));
                }

                List<TemplateEntity> all = query.OrderBy(template => template.CreatedAt).ThenBy(template => template.Id).ToList();
                return Task.FromResult(result: new PagedResult<TemplateEntity>
                {
                        Items = all.Skip(count: (page - 1) * pageSize).Take(count: pageSize).ToList(),
                        Page = page,
                        PageSize = pageSize,
                        TotalCount = all.Count
                });
        }

        public Task AddAsync(TemplateEntity template, CancellationToken cancellationToken)
        {
                this._state.Templates[template.Id] = template;
                return Task.CompletedTask;
        }

        public Task UpdateAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
        {
                this._state.Templates[template.Id] = template;
                return Task.CompletedTask;
        }

        public Task DeleteAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken)
        {
                this._state.Templates[template.Id] = template;
                return Task.CompletedTask;
        }
}

