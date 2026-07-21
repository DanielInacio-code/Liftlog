using LiftLog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace LiftLog.Core.Data;

public sealed class DatabaseInitializer(IDbContextFactory<LiftLogDbContext> contextFactory)
    : IDatabaseInitializer
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_initialized)
            {
                return;
            }

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await context.Database.EnsureCreatedAsync(cancellationToken);
            await UpgradeSchemaAsync(context, cancellationToken);
            await TranslatePredefinedExercisesAsync(context, cancellationToken);

            var existingExercises = await context.Exercises
                .AsNoTracking()
                .Select(exercise => new { exercise.Name, exercise.NormalizedName })
                .ToListAsync(cancellationToken);

            var existing = existingExercises
                .SelectMany(exercise => new[] { exercise.Name, exercise.NormalizedName })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(NormalizeName)
                .ToHashSet(StringComparer.Ordinal);
            var missing = SeedExercises.Create()
                .Where(exercise => !existing.Contains(NormalizeName(exercise.Name)))
                .ToList();

            if (missing.Count > 0)
            {
                context.Exercises.AddRange(missing);
                await context.SaveChangesAsync(cancellationToken);
            }

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task TranslatePredefinedExercisesAsync(
        LiftLogDbContext context,
        CancellationToken cancellationToken)
    {
        var predefinedExercises = await context.Exercises
            .Where(exercise => !exercise.IsCustom)
            .ToListAsync(cancellationToken);

        var historicalExercises = await context.WorkoutExercises
            .Where(exercise => exercise.ExerciseId != null)
            .ToListAsync(cancellationToken);

        var routineExercises = await context.RoutineExercises
            .Include(exercise => exercise.Sets)
            .ToListAsync(cancellationToken);

        var changed = RetirePredefinedExercises(
            context,
            predefinedExercises,
            routineExercises,
            historicalExercises);

        foreach (var translation in SeedExercises.LegacyNameTranslations)
        {
            var legacy = predefinedExercises.FirstOrDefault(exercise =>
                string.Equals(exercise.Name, translation.Key, StringComparison.OrdinalIgnoreCase));
            var english = predefinedExercises.FirstOrDefault(exercise =>
                string.Equals(exercise.Name, translation.Value, StringComparison.OrdinalIgnoreCase));

            if (legacy is not null && english is null)
            {
                var englishNameTaken = await context.Exercises.AnyAsync(
                    exercise => exercise.Id != legacy.Id &&
                                exercise.NormalizedName == translation.Value.ToUpperInvariant(),
                    cancellationToken);

                if (!englishNameTaken)
                {
                    legacy.Name = translation.Value;
                    legacy.NormalizedName = translation.Value.ToUpperInvariant();
                    english = legacy;
                    changed = true;
                }
            }
            else if (legacy is not null && english is not null && legacy.Id != english.Id)
            {
                MergePredefinedExercises(
                    context,
                    legacy,
                    english,
                    routineExercises,
                    historicalExercises);
                predefinedExercises.Remove(legacy);
                changed = true;
            }

            if (english is null)
            {
                continue;
            }

            foreach (var historical in historicalExercises.Where(exercise =>
                         exercise.ExerciseId == english.Id &&
                         string.Equals(exercise.ExerciseName, translation.Key, StringComparison.OrdinalIgnoreCase)))
            {
                historical.ExerciseName = translation.Value;
                changed = true;
            }
        }

        var canonicalExercises = SeedExercises.Create()
            .ToDictionary(exercise => exercise.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var predefined in predefinedExercises)
        {
            if (!canonicalExercises.TryGetValue(predefined.Name, out var canonical) ||
                predefined.MuscleGroup == canonical.MuscleGroup &&
                predefined.Equipment == canonical.Equipment)
            {
                continue;
            }

            predefined.MuscleGroup = canonical.MuscleGroup;
            predefined.Equipment = canonical.Equipment;
            changed = true;
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static bool RetirePredefinedExercises(
        LiftLogDbContext context,
        List<Exercise> predefinedExercises,
        List<RoutineExercise> routineExercises,
        List<WorkoutExercise> historicalExercises)
    {
        var retiredNames = SeedExercises.RetiredPredefinedNames
            .Select(NormalizeName)
            .ToHashSet(StringComparer.Ordinal);
        var retiredExercises = predefinedExercises
            .Where(exercise =>
                retiredNames.Contains(NormalizeName(exercise.Name)) ||
                retiredNames.Contains(NormalizeName(exercise.NormalizedName)))
            .ToList();

        if (retiredExercises.Count == 0)
        {
            return false;
        }

        var affectedRoutineIds = new HashSet<int>();
        foreach (var retired in retiredExercises)
        {
            foreach (var historical in historicalExercises.Where(item =>
                         item.ExerciseId == retired.Id))
            {
                historical.ExerciseId = null;
                historical.Exercise = null;
            }

            foreach (var routineExercise in routineExercises
                         .Where(item => item.ExerciseId == retired.Id)
                         .ToList())
            {
                affectedRoutineIds.Add(routineExercise.RoutineId);
                context.RoutineExercises.Remove(routineExercise);
                routineExercises.Remove(routineExercise);
            }

            context.Exercises.Remove(retired);
            predefinedExercises.Remove(retired);
        }

        foreach (var routineId in affectedRoutineIds)
        {
            var ordered = routineExercises
                .Where(item => item.RoutineId == routineId)
                .OrderBy(item => item.Position)
                .ThenBy(item => item.Id)
                .ToList();

            for (var position = 0; position < ordered.Count; position++)
            {
                ordered[position].Position = position;
            }
        }

        return true;
    }

    private static void MergePredefinedExercises(
        LiftLogDbContext context,
        Exercise legacy,
        Exercise canonical,
        List<RoutineExercise> routineExercises,
        List<WorkoutExercise> historicalExercises)
    {
        var affectedRoutineIds = new HashSet<int>();
        var legacyRoutineExercises = routineExercises
            .Where(item => item.ExerciseId == legacy.Id)
            .ToList();

        foreach (var legacyRoutineExercise in legacyRoutineExercises)
        {
            affectedRoutineIds.Add(legacyRoutineExercise.RoutineId);
            var canonicalRoutineExercise = routineExercises.FirstOrDefault(item =>
                item.RoutineId == legacyRoutineExercise.RoutineId &&
                item.ExerciseId == canonical.Id);

            if (canonicalRoutineExercise is null)
            {
                legacyRoutineExercise.ExerciseId = canonical.Id;
                legacyRoutineExercise.Exercise = canonical;
                continue;
            }

            canonicalRoutineExercise.Position = Math.Min(
                canonicalRoutineExercise.Position,
                legacyRoutineExercise.Position);

            var nextSetNumber = canonicalRoutineExercise.Sets
                .Select(item => item.SetNumber)
                .DefaultIfEmpty()
                .Max() + 1;

            foreach (var routineSet in legacyRoutineExercise.Sets
                         .OrderBy(item => item.SetNumber)
                         .ToList())
            {
                routineSet.RoutineExerciseId = canonicalRoutineExercise.Id;
                routineSet.RoutineExercise = canonicalRoutineExercise;
                routineSet.SetNumber = nextSetNumber++;
            }

            context.RoutineExercises.Remove(legacyRoutineExercise);
            routineExercises.Remove(legacyRoutineExercise);
        }

        foreach (var routineId in affectedRoutineIds)
        {
            var ordered = routineExercises
                .Where(item => item.RoutineId == routineId)
                .OrderBy(item => item.Position)
                .ThenBy(item => item.Id)
                .ToList();

            for (var position = 0; position < ordered.Count; position++)
            {
                ordered[position].Position = position;
            }
        }

        foreach (var historical in historicalExercises.Where(item => item.ExerciseId == legacy.Id))
        {
            historical.ExerciseId = canonical.Id;
            historical.Exercise = canonical;
            historical.ExerciseName = canonical.Name;
        }

        context.Exercises.Remove(legacy);
    }

    private static string NormalizeName(string name) =>
        name.Trim().ToUpperInvariant();

    private static async Task UpgradeSchemaAsync(
        LiftLogDbContext context,
        CancellationToken cancellationToken)
    {
        await EnsureExerciseImageColumnAsync(context, cancellationToken);

        // EnsureCreated does not add tables to an existing database. These idempotent
        // statements upgrade databases from earlier phases without deleting user data.
        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "Routines" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_Routines" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT COLLATE NOCASE NOT NULL,
                "NormalizedName" TEXT NOT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "UpdatedAt" INTEGER NOT NULL,
                CONSTRAINT "CK_Routines_Name_Length" CHECK (length(trim(Name)) BETWEEN 2 AND 100)
            );
            """,
            cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "RoutineExercises" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RoutineExercises" PRIMARY KEY AUTOINCREMENT,
                "RoutineId" INTEGER NOT NULL,
                "ExerciseId" INTEGER NOT NULL,
                "Position" INTEGER NOT NULL,
                CONSTRAINT "CK_RoutineExercises_Position" CHECK (Position >= 0),
                CONSTRAINT "FK_RoutineExercises_Routines_RoutineId" FOREIGN KEY ("RoutineId") REFERENCES "Routines" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_RoutineExercises_Exercises_ExerciseId" FOREIGN KEY ("ExerciseId") REFERENCES "Exercises" ("Id") ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Routines_NormalizedName\" ON \"Routines\" (\"NormalizedName\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_Routines_UpdatedAt\" ON \"Routines\" (\"UpdatedAt\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_RoutineExercises_ExerciseId\" ON \"RoutineExercises\" (\"ExerciseId\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_RoutineExercises_RoutineId_ExerciseId\" ON \"RoutineExercises\" (\"RoutineId\", \"ExerciseId\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_RoutineExercises_RoutineId_Position\" ON \"RoutineExercises\" (\"RoutineId\", \"Position\");",
            cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "RoutineSets" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_RoutineSets" PRIMARY KEY AUTOINCREMENT,
                "RoutineExerciseId" INTEGER NOT NULL,
                "SetNumber" INTEGER NOT NULL,
                "Weight" INTEGER NOT NULL,
                "Repetitions" INTEGER NOT NULL,
                "IsWarmup" INTEGER NOT NULL,
                "SetType" TEXT NOT NULL DEFAULT 'Normal',
                "Rpe" REAL NULL,
                CONSTRAINT "CK_RoutineSets_SetNumber" CHECK (SetNumber > 0),
                CONSTRAINT "CK_RoutineSets_Weight" CHECK (Weight >= 0),
                CONSTRAINT "CK_RoutineSets_Repetitions" CHECK (Repetitions >= 0),
                CONSTRAINT "CK_RoutineSets_Rpe" CHECK (Rpe IS NULL OR Rpe BETWEEN 0 AND 10),
                CONSTRAINT "FK_RoutineSets_RoutineExercises_RoutineExerciseId" FOREIGN KEY ("RoutineExerciseId") REFERENCES "RoutineExercises" ("Id") ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_RoutineSets_RoutineExerciseId_SetNumber\" ON \"RoutineSets\" (\"RoutineExerciseId\", \"SetNumber\");",
            cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "WorkoutSessions" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkoutSessions" PRIMARY KEY AUTOINCREMENT,
                "RoutineId" INTEGER NULL,
                "RoutineName" TEXT NOT NULL,
                "StartedAt" INTEGER NOT NULL,
                "FinishedAt" INTEGER NULL,
                "Notes" TEXT NULL,
                "Status" TEXT NOT NULL,
                CONSTRAINT "FK_WorkoutSessions_Routines_RoutineId" FOREIGN KEY ("RoutineId") REFERENCES "Routines" ("Id") ON DELETE SET NULL
            );
            """,
            cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "WorkoutExercises" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkoutExercises" PRIMARY KEY AUTOINCREMENT,
                "WorkoutSessionId" INTEGER NOT NULL,
                "ExerciseId" INTEGER NULL,
                "ExerciseName" TEXT NOT NULL,
                "Position" INTEGER NOT NULL,
                "Notes" TEXT NULL,
                "RestTimerSeconds" INTEGER NOT NULL DEFAULT 0,
                CONSTRAINT "CK_WorkoutExercises_Position" CHECK (Position >= 0),
                CONSTRAINT "FK_WorkoutExercises_WorkoutSessions_WorkoutSessionId" FOREIGN KEY ("WorkoutSessionId") REFERENCES "WorkoutSessions" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_WorkoutExercises_Exercises_ExerciseId" FOREIGN KEY ("ExerciseId") REFERENCES "Exercises" ("Id") ON DELETE SET NULL
            );
            """,
            cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "WorkoutSets" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_WorkoutSets" PRIMARY KEY AUTOINCREMENT,
                "WorkoutExerciseId" INTEGER NOT NULL,
                "SetNumber" INTEGER NOT NULL,
                "Weight" INTEGER NOT NULL,
                "Repetitions" INTEGER NOT NULL,
                "IsWarmup" INTEGER NOT NULL,
                "SetType" TEXT NOT NULL DEFAULT 'Normal',
                "Rpe" REAL NULL,
                "IsCompleted" INTEGER NOT NULL,
                "CompletedAt" INTEGER NULL,
                CONSTRAINT "CK_WorkoutSets_SetNumber" CHECK (SetNumber > 0),
                CONSTRAINT "CK_WorkoutSets_Weight" CHECK (Weight >= 0),
                CONSTRAINT "CK_WorkoutSets_Repetitions" CHECK (Repetitions >= 0),
                CONSTRAINT "CK_WorkoutSets_Rpe" CHECK (Rpe IS NULL OR Rpe BETWEEN 0 AND 10),
                CONSTRAINT "FK_WorkoutSets_WorkoutExercises_WorkoutExerciseId" FOREIGN KEY ("WorkoutExerciseId") REFERENCES "WorkoutExercises" ("Id") ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WorkoutSessions_RoutineId\" ON \"WorkoutSessions\" (\"RoutineId\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WorkoutSessions_StartedAt\" ON \"WorkoutSessions\" (\"StartedAt\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_WorkoutSessions_Status\" ON \"WorkoutSessions\" (\"Status\") WHERE \"Status\" = 'InProgress';",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WorkoutSessions_Status_StartedAt\" ON \"WorkoutSessions\" (\"Status\", \"StartedAt\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WorkoutSessions_Status_FinishedAt\" ON \"WorkoutSessions\" (\"Status\", \"FinishedAt\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WorkoutExercises_ExerciseId\" ON \"WorkoutExercises\" (\"ExerciseId\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WorkoutExercises_ExerciseId_WorkoutSessionId\" ON \"WorkoutExercises\" (\"ExerciseId\", \"WorkoutSessionId\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WorkoutExercises_WorkoutSessionId_Position\" ON \"WorkoutExercises\" (\"WorkoutSessionId\", \"Position\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WorkoutSets_WorkoutExerciseId_SetNumber\" ON \"WorkoutSets\" (\"WorkoutExerciseId\", \"SetNumber\");",
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_WorkoutSets_WorkoutExerciseId_CompletedAt\" ON \"WorkoutSets\" (\"WorkoutExerciseId\", \"CompletedAt\");",
            cancellationToken);

        await context.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "BodyMeasurements" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_BodyMeasurements" PRIMARY KEY AUTOINCREMENT,
                "RecordedAt" INTEGER NOT NULL,
                "WeightKg" INTEGER NULL,
                "BodyFatPercentage" INTEGER NULL,
                "WaistCm" INTEGER NULL,
                "Notes" TEXT NULL,
                CONSTRAINT "CK_BodyMeasurements_WeightKg" CHECK (WeightKg IS NULL OR WeightKg BETWEEN 1 AND 1000000),
                CONSTRAINT "CK_BodyMeasurements_BodyFatPercentage" CHECK (BodyFatPercentage IS NULL OR BodyFatPercentage BETWEEN 0 AND 100000),
                CONSTRAINT "CK_BodyMeasurements_WaistCm" CHECK (WaistCm IS NULL OR WaistCm BETWEEN 1 AND 500000)
            );
            """,
            cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS \"IX_BodyMeasurements_RecordedAt\" ON \"BodyMeasurements\" (\"RecordedAt\");",
            cancellationToken);

        await EnsureColumnAsync(
            context,
            "WorkoutExercises",
            "RestTimerSeconds",
            "ALTER TABLE \"WorkoutExercises\" ADD COLUMN \"RestTimerSeconds\" INTEGER NOT NULL DEFAULT 0;",
            cancellationToken);
        await EnsureSetMetadataColumnsAsync(context, cancellationToken);
    }

    private static async Task EnsureExerciseImageColumnAsync(
        LiftLogDbContext context,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM pragma_table_info('Exercises') WHERE name = 'ImagePath';";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (Convert.ToInt32(result) == 0)
            {
                command.CommandText = "ALTER TABLE \"Exercises\" ADD COLUMN \"ImagePath\" TEXT NULL;";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureSetMetadataColumnsAsync(
        LiftLogDbContext context,
        CancellationToken cancellationToken)
    {
        foreach (var tableName in new[] { "RoutineSets", "WorkoutSets" })
        {
            await EnsureColumnAsync(
                context,
                tableName,
                "SetType",
                $"ALTER TABLE \"{tableName}\" ADD COLUMN \"SetType\" TEXT NOT NULL DEFAULT 'Normal';",
                cancellationToken);
            await EnsureColumnAsync(
                context,
                tableName,
                "Rpe",
                $"ALTER TABLE \"{tableName}\" ADD COLUMN \"Rpe\" REAL NULL;",
                cancellationToken);

            var updateStatement = tableName == "RoutineSets"
                ? "UPDATE \"RoutineSets\" SET \"SetType\" = 'Warmup' WHERE \"IsWarmup\" = 1 AND \"SetType\" = 'Normal';"
                : "UPDATE \"WorkoutSets\" SET \"SetType\" = 'Warmup' WHERE \"IsWarmup\" = 1 AND \"SetType\" = 'Normal';";
            await context.Database.ExecuteSqlRawAsync(updateStatement, cancellationToken);
        }
    }

    private static async Task EnsureColumnAsync(
        LiftLogDbContext context,
        string tableName,
        string columnName,
        string alterStatement,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name = '{columnName}';";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (Convert.ToInt32(result) == 0)
            {
                command.CommandText = alterStatement;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
