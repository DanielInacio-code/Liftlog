using LiftLog.Core.Data;
using LiftLog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LiftLog.Core.Services;

public sealed class ExerciseService(
    IDbContextFactory<LiftLogDbContext> contextFactory,
    IDatabaseInitializer databaseInitializer) : IExerciseService
{
    public async Task<IReadOnlyList<Exercise>> GetAllAsync(
        string? searchText = null,
        MuscleGroup? muscleGroup = null,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.Exercises.AsNoTracking();

        var normalizedSearch = NormalizeSearchText(searchText);
        if (!string.IsNullOrEmpty(normalizedSearch))
        {
            var matchingMuscleGroups = FindMatchingMuscleGroups(normalizedSearch);
            if (matchingMuscleGroups.Length > 0)
            {
                query = query.Where(exercise => matchingMuscleGroups.Contains(exercise.MuscleGroup));
            }
            else
            {
                query = query.Where(exercise => EF.Functions.Like(
                    exercise.Name
                        .ToUpper()
                        .Replace("-", " ")
                        .Replace("/", " ")
                        .Replace("SUPPORTED", "SUPPORT")
                        .Replace("  ", " ")
                        .Replace("  ", " "),
                    $"%{normalizedSearch}%"));
            }
        }

        if (muscleGroup is not null)
        {
            query = query.Where(exercise => exercise.MuscleGroup == muscleGroup.Value);
        }

        return await query
            .OrderBy(exercise => exercise.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Exercise?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Exercises
            .AsNoTracking()
            .SingleOrDefaultAsync(exercise => exercise.Id == id, cancellationToken);
    }

    public async Task<Exercise> CreateCustomAsync(
        ExerciseInput input,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        var normalized = NormalizeAndValidate(input);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await EnsureNameIsUniqueAsync(context, normalized.Name, null, cancellationToken);

        var exercise = new Exercise
        {
            Name = normalized.Name,
            NormalizedName = NormalizeName(normalized.Name),
            MuscleGroup = normalized.MuscleGroup,
            Equipment = normalized.Equipment,
            Instructions = normalized.Instructions,
            ImagePath = normalized.ImagePath,
            IsCustom = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Exercises.Add(exercise);
        await SaveChangesAsync(context, cancellationToken);

        return exercise;
    }

    public async Task<Exercise> UpdateCustomAsync(
        int id,
        ExerciseInput input,
        CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        var normalized = NormalizeAndValidate(input);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exercise = await context.Exercises
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ExerciseValidationException("The exercise was not found.");

        if (!exercise.IsCustom)
        {
            throw new ExerciseValidationException("Built-in exercises cannot be edited.");
        }

        await EnsureNameIsUniqueAsync(context, normalized.Name, id, cancellationToken);

        exercise.Name = normalized.Name;
        exercise.NormalizedName = NormalizeName(normalized.Name);
        exercise.MuscleGroup = normalized.MuscleGroup;
        exercise.Equipment = normalized.Equipment;
        exercise.Instructions = normalized.Instructions;
        exercise.ImagePath = normalized.ImagePath;

        await SaveChangesAsync(context, cancellationToken);
        return exercise;
    }

    public async Task DeleteCustomAsync(int id, CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var exercise = await context.Exercises
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new ExerciseValidationException("The exercise was not found.");

        if (!exercise.IsCustom)
        {
            throw new ExerciseValidationException("Built-in exercises cannot be deleted.");
        }

        context.Exercises.Remove(exercise);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static ExerciseInput NormalizeAndValidate(ExerciseInput input)
    {
        var name = input.Name?.Trim() ?? string.Empty;

        if (name.Length is < 2 or > 100)
        {
            throw new ExerciseValidationException("The name must be between 2 and 100 characters.");
        }

        var instructions = string.IsNullOrWhiteSpace(input.Instructions)
            ? null
            : input.Instructions.Trim();

        if (instructions?.Length > 2000)
        {
            throw new ExerciseValidationException("Instructions cannot exceed 2,000 characters.");
        }

        var imagePath = string.IsNullOrWhiteSpace(input.ImagePath)
            ? null
            : input.ImagePath.Trim();

        if (imagePath?.Length > 1024)
        {
            throw new ExerciseValidationException("The image path is too long.");
        }

        return input with { Name = name, Instructions = instructions, ImagePath = imagePath };
    }

    private static async Task EnsureNameIsUniqueAsync(
        LiftLogDbContext context,
        string name,
        int? excludedId,
        CancellationToken cancellationToken)
    {
        var normalizedName = NormalizeName(name);
        var duplicateExists = await context.Exercises.AnyAsync(
            exercise => exercise.NormalizedName == normalizedName &&
                        (!excludedId.HasValue || exercise.Id != excludedId.Value),
            cancellationToken);

        if (duplicateExists)
        {
            throw new ExerciseValidationException("An exercise with this name already exists.");
        }
    }

    private static string NormalizeName(string name) => name.ToUpperInvariant();

    private static string NormalizeSearchText(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return string.Empty;
        }

        var normalized = searchText
            .Trim()
            .ToUpperInvariant()
            .Replace('-', ' ')
            .Replace('/', ' ')
            .Replace("SUPPORTED", "SUPPORT");

        return string.Join(' ', normalized.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static MuscleGroup[] FindMatchingMuscleGroups(string normalizedSearch)
    {
        if (normalizedSearch.Length < 3)
        {
            return [];
        }

        var compactSearch = normalizedSearch.Replace(" ", string.Empty);
        return Enum.GetValues<MuscleGroup>()
            .Where(group => group
                .ToString()
                .ToUpperInvariant()
                .StartsWith(compactSearch, StringComparison.Ordinal))
            .ToArray();
    }

    private static async Task SaveChangesAsync(
        LiftLogDbContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            throw new ExerciseValidationException(
                "The exercise could not be saved. Check whether the name already exists.",
                exception);
        }
    }
}
