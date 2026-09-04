using MediatR;
using Microsoft.EntityFrameworkCore;
using PeopleHQ.Application.Common.Exceptions;
using PeopleHQ.Application.Common.Interfaces;
using PeopleHQ.Application.Employees;
using PeopleHQ.Domain.Employees;
using PeopleHQ.Infrastructure.Persistence;

namespace PeopleHQ.Infrastructure.Employees;

public class CreateCustomFieldDefinitionCommandHandler : IRequestHandler<CreateCustomFieldDefinitionCommand, Guid>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public CreateCustomFieldDefinitionCommandHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<Guid> Handle(CreateCustomFieldDefinitionCommand request, CancellationToken ct)
    {
        var definition = new CustomFieldDefinition
        {
            TenantId = _tenant.TenantId,
            Entity = request.Entity,
            Label = request.Label,
            FieldType = request.FieldType,
            OptionsJson = request.OptionsJson,
            IsRequired = request.IsRequired
        };
        _db.CustomFieldDefinitions.Add(definition);
        await _db.SaveChangesAsync(ct);
        return definition.Id;
    }
}

public class UpdateCustomFieldDefinitionCommandHandler : IRequestHandler<UpdateCustomFieldDefinitionCommand>
{
    private readonly AppDbContext _db;
    public UpdateCustomFieldDefinitionCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(UpdateCustomFieldDefinitionCommand request, CancellationToken ct)
    {
        var definition = await _db.CustomFieldDefinitions.FindAsync(new object[] { request.Id }, ct)
            ?? throw new NotFoundException(nameof(CustomFieldDefinition), request.Id);
        definition.Label = request.Label;
        definition.OptionsJson = request.OptionsJson;
        definition.IsRequired = request.IsRequired;
        await _db.SaveChangesAsync(ct);
    }
}

public class DeleteCustomFieldDefinitionCommandHandler : IRequestHandler<DeleteCustomFieldDefinitionCommand>
{
    private readonly AppDbContext _db;
    public DeleteCustomFieldDefinitionCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(DeleteCustomFieldDefinitionCommand request, CancellationToken ct)
    {
        var definition = await _db.CustomFieldDefinitions.FindAsync(new object[] { request.Id }, ct)
            ?? throw new NotFoundException(nameof(CustomFieldDefinition), request.Id);

        var inUse = await _db.EmployeeCustomFieldValues.AnyAsync(v => v.FieldDefinitionId == request.Id, ct);
        if (inUse) throw new ConflictException("This custom field has values recorded against it and cannot be deleted.");

        definition.IsDeleted = true;
        definition.DeletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}

public class GetCustomFieldDefinitionsQueryHandler : IRequestHandler<GetCustomFieldDefinitionsQuery, IReadOnlyList<CustomFieldDefinitionDto>>
{
    private readonly AppDbContext _db;
    public GetCustomFieldDefinitionsQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CustomFieldDefinitionDto>> Handle(GetCustomFieldDefinitionsQuery request, CancellationToken ct)
        => await _db.CustomFieldDefinitions
            .Where(d => d.Entity == request.Entity)
            .Select(d => new CustomFieldDefinitionDto(d.Id, d.Entity, d.Label, d.FieldType, d.OptionsJson, d.IsRequired))
            .ToListAsync(ct);
}

public class SetEmployeeCustomFieldValuesCommandHandler : IRequestHandler<SetEmployeeCustomFieldValuesCommand>
{
    private readonly AppDbContext _db;
    public SetEmployeeCustomFieldValuesCommandHandler(AppDbContext db) => _db = db;

    public async Task Handle(SetEmployeeCustomFieldValuesCommand request, CancellationToken ct)
    {
        _ = await _db.Employees.FindAsync(new object[] { request.EmployeeId }, ct)
            ?? throw new NotFoundException(nameof(Employee), request.EmployeeId);

        var existing = await _db.EmployeeCustomFieldValues.Where(v => v.EmployeeId == request.EmployeeId).ToListAsync(ct);
        var existingByField = existing.ToDictionary(v => v.FieldDefinitionId);

        foreach (var input in request.Values)
        {
            if (existingByField.TryGetValue(input.FieldDefinitionId, out var row))
            {
                row.Value = input.Value;
            }
            else
            {
                _db.EmployeeCustomFieldValues.Add(new EmployeeCustomFieldValue
                {
                    EmployeeId = request.EmployeeId,
                    FieldDefinitionId = input.FieldDefinitionId,
                    Value = input.Value
                });
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}

public class GetEmployeeCustomFieldValuesQueryHandler : IRequestHandler<GetEmployeeCustomFieldValuesQuery, IReadOnlyList<CustomFieldValueInput>>
{
    private readonly AppDbContext _db;
    public GetEmployeeCustomFieldValuesQueryHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<CustomFieldValueInput>> Handle(GetEmployeeCustomFieldValuesQuery request, CancellationToken ct)
        => await _db.EmployeeCustomFieldValues
            .Where(v => v.EmployeeId == request.EmployeeId)
            .Select(v => new CustomFieldValueInput(v.FieldDefinitionId, v.Value))
            .ToListAsync(ct);
}
