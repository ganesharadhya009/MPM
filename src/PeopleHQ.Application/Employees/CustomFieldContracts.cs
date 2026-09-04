using MediatR;
using PeopleHQ.Domain.Employees;

namespace PeopleHQ.Application.Employees;

// Employee custom fields (01-modules-functional-spec.md §D/§C "EAV-lite" note). CustomFieldDefinition.Entity
// is a free-text discriminator (currently only "Employee" is used) so the same table can extend other
// entities later without a migration. OptionsJson holds the JSON array of choices for Dropdown.

public record CreateCustomFieldDefinitionCommand(string Entity, string Label, CustomFieldType FieldType, string? OptionsJson, bool IsRequired) : IRequest<Guid>;
public record UpdateCustomFieldDefinitionCommand(Guid Id, string Label, string? OptionsJson, bool IsRequired) : IRequest;
public record DeleteCustomFieldDefinitionCommand(Guid Id) : IRequest;
public record GetCustomFieldDefinitionsQuery(string Entity = "Employee") : IRequest<IReadOnlyList<CustomFieldDefinitionDto>>;
public record CustomFieldDefinitionDto(Guid Id, string Entity, string Label, CustomFieldType FieldType, string? OptionsJson, bool IsRequired);

public record CustomFieldValueInput(Guid FieldDefinitionId, string? Value);
public record SetEmployeeCustomFieldValuesCommand(Guid EmployeeId, IReadOnlyList<CustomFieldValueInput> Values) : IRequest;
public record GetEmployeeCustomFieldValuesQuery(Guid EmployeeId) : IRequest<IReadOnlyList<CustomFieldValueInput>>;
