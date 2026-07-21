using LiftLog.Core.Data;
using LiftLog.Core.Models;
using LiftLog.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LiftLog.Tests;

public class DatabaseInitializerTests
{
    [Fact]
    public async Task InitializeDatabase_ExistingSchema_AddsBodyMeasurementsTable()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);

        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();
            await setupContext.Database.ExecuteSqlRawAsync("DROP TABLE \"BodyMeasurements\";");
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'BodyMeasurements';";
        var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync());

        Assert.Equal(1, tableCount);
    }

    [Fact]
    public async Task InitializeDatabase_ExistingSchema_AddsCompletedHistoryPagingIndex()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);

        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();
            await setupContext.Database.ExecuteSqlRawAsync(
                "DROP INDEX IF EXISTS \"IX_WorkoutSessions_Status_StartedAt\";");
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM pragma_index_list('WorkoutSessions') " +
            "WHERE name = 'IX_WorkoutSessions_Status_StartedAt';";
        var indexCount = Convert.ToInt32(await command.ExecuteScalarAsync());

        Assert.Equal(1, indexCount);
    }

    [Fact]
    public async Task InitializeDatabase_WhenCalledRepeatedly_DoesNotDuplicateSeedExercises()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var firstResult = await database.ExerciseService.GetAllAsync();
        var secondResult = await database.ExerciseService.GetAllAsync();

        Assert.Equal(48, firstResult.Count);
        Assert.Equal(48, secondResult.Count);
        Assert.All(secondResult, exercise => Assert.False(exercise.IsCustom));
        Assert.Contains(secondResult, exercise =>
            exercise.Name == "Barbell bench press" &&
            exercise.MuscleGroup == MuscleGroup.Chest);
        Assert.Contains(secondResult, exercise =>
            exercise.Name == "Chest Press - Neutral Grip (Machine)" &&
            exercise.MuscleGroup == MuscleGroup.Chest &&
            exercise.Equipment == Equipment.Machine);

        string[] hevyExerciseNames =
        [
            "Triceps Pushdown",
            "Single-Arm Triceps Pushdown",
            "Preacher Curl (Dumbbell)",
            "Lying Leg Curl",
            "Seated Leg Curl",
            "Calf Extension (Machine)",
            "Leg Extension (Machine)",
            "Chest Press (Machine)",
            "Bent Over Row",
            "Chest Fly (Machine)",
            "Lat Pulldown (Cable)",
            "Chest Dip",
            "Lateral Raise (Machine)",
            "Seated Incline Curl (Dumbbell)",
            "Hammer Curl (Dumbbell)",
            "Leg Press (Machine)",
            "Straight Leg Deadlift",
            "Chest Supported Row / T-Bar Row",
            "Shoulder Press (Dumbbell)",
            "Pullover (Machine)",
            "Straight-Arm Pulldown",
            "Single Arm Cable Row",
            "Lateral Raise (Cable)",
            "Single Leg Press (Machine)",
            "Overhead Triceps Extension (Cable)",
            "Hip Adduction (Machine)",
            "Preacher Curl (Machine)",
            "Wide Row (Machine)",
            "Lat Pulldown - Close Grip (Cable)",
            "Lateral Raise (Dumbbell)",
            "Shoulder Press (Machine)",
            "Butterfly (Pec Deck)",
            "Bayesian Curl (Cable)",
            "Hip Abduction (Machine)",
            "Incline Bench Press (Dumbbell)",
            "Decline Bench Press (Machine)",
            "Rear Delt Reverse Fly (Cable)",
            "Preacher Curl (Barbell)",
            "Hack Squat (Machine)",
            "Seated Cable Row - V Grip (Cable)",
            "Face Pull",
            "Romanian Deadlift (Barbell)"
        ];

        Assert.All(hevyExerciseNames, expectedName =>
            Assert.Contains(secondResult, exercise => exercise.Name == expectedName));
        Assert.DoesNotContain(secondResult, exercise =>
            exercise.Name == "Remada Aberta Maquina" ||
            exercise.Name == "Seated Leg Curl (Machine)" ||
            exercise.Name == "Seated Shoulder Press (Machine)" ||
            exercise.Name == "Leg curl" ||
            exercise.Name == "Cable triceps extension" ||
            exercise.Name == "Triceps Extension (Dumbbell)" ||
            exercise.Name == "Seated Overhead Press (Dumbbell)" ||
            exercise.Name == "Chest Supported Incline Row (Dumbbell)" ||
            exercise.Name == "Plank");
    }

    [Fact]
    public async Task InitializeDatabase_WithSeatedLegCurl_RenamesItAndPreservesRelationships()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);
        var timestamp = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

        int exerciseId;
        int routineId;
        int sessionId;
        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();

            var seatedLegCurl = new Exercise
            {
                Name = "Seated Leg Curl (Machine)",
                NormalizedName = "SEATED LEG CURL (MACHINE)",
                MuscleGroup = MuscleGroup.Legs,
                Equipment = Equipment.Machine,
                IsCustom = false,
                CreatedAt = timestamp
            };
            var routine = new Routine
            {
                Name = "Leg day",
                NormalizedName = "LEG DAY",
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Exercises =
                [
                    new RoutineExercise { Exercise = seatedLegCurl, Position = 0 }
                ]
            };
            var session = new WorkoutSession
            {
                Routine = routine,
                RoutineName = routine.Name,
                StartedAt = timestamp,
                FinishedAt = timestamp.AddMinutes(30),
                Status = WorkoutStatus.Completed,
                Exercises =
                [
                    new WorkoutExercise
                    {
                        Exercise = seatedLegCurl,
                        ExerciseName = seatedLegCurl.Name,
                        Position = 0
                    }
                ]
            };

            setupContext.WorkoutSessions.Add(session);
            await setupContext.SaveChangesAsync();
            exerciseId = seatedLegCurl.Id;
            routineId = routine.Id;
            sessionId = session.Id;
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var renamed = await verificationContext.Exercises.SingleAsync(item => item.Id == exerciseId);
        var routineExercise = await verificationContext.RoutineExercises
            .SingleAsync(item => item.RoutineId == routineId);
        var workoutExercise = await verificationContext.WorkoutExercises
            .SingleAsync(item => item.WorkoutSessionId == sessionId);

        Assert.Equal("Seated Leg Curl", renamed.Name);
        Assert.Equal("SEATED LEG CURL", renamed.NormalizedName);
        Assert.Equal(exerciseId, routineExercise.ExerciseId);
        Assert.Equal(exerciseId, workoutExercise.ExerciseId);
        Assert.Equal("Seated Leg Curl", workoutExercise.ExerciseName);
        Assert.False(await verificationContext.Exercises.AnyAsync(exercise =>
            !exercise.IsCustom && exercise.Name == "Seated Leg Curl (Machine)"));
        Assert.Equal(48, await verificationContext.Exercises.CountAsync(exercise => !exercise.IsCustom));
    }

    [Fact]
    public async Task InitializeDatabase_WithRetiredPlank_RemovesItAndPreservesWorkoutHistory()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);
        var timestamp = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

        int plankId;
        int routineId;
        int routineSetId;
        int sessionId;
        int workoutSetId;
        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();

            var plank = new Exercise
            {
                Name = "Plank",
                NormalizedName = "PLANK",
                MuscleGroup = MuscleGroup.Core,
                Equipment = Equipment.Bodyweight,
                IsCustom = false,
                CreatedAt = timestamp
            };
            var deadlift = new Exercise
            {
                Name = "Deadlift",
                NormalizedName = "DEADLIFT",
                MuscleGroup = MuscleGroup.Back,
                Equipment = Equipment.Barbell,
                IsCustom = false,
                CreatedAt = timestamp
            };
            var plankRoutineSet = new RoutineSet
            {
                SetNumber = 1,
                Weight = 0,
                Repetitions = 60,
                SetType = TrainingSetType.Normal
            };
            var routine = new Routine
            {
                Name = "Core and back",
                NormalizedName = "CORE AND BACK",
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Exercises =
                [
                    new RoutineExercise
                    {
                        Exercise = plank,
                        Position = 0,
                        Sets = [plankRoutineSet]
                    },
                    new RoutineExercise { Exercise = deadlift, Position = 1 }
                ]
            };
            var plankWorkoutSet = new WorkoutSet
            {
                SetNumber = 1,
                Weight = 0,
                Repetitions = 60,
                SetType = TrainingSetType.Normal,
                IsCompleted = true,
                CompletedAt = timestamp.AddMinutes(1)
            };
            var session = new WorkoutSession
            {
                Routine = routine,
                RoutineName = routine.Name,
                StartedAt = timestamp,
                FinishedAt = timestamp.AddMinutes(30),
                Status = WorkoutStatus.Completed,
                Exercises =
                [
                    new WorkoutExercise
                    {
                        Exercise = plank,
                        ExerciseName = plank.Name,
                        Position = 0,
                        Sets = [plankWorkoutSet]
                    }
                ]
            };

            setupContext.WorkoutSessions.Add(session);
            await setupContext.SaveChangesAsync();
            plankId = plank.Id;
            routineId = routine.Id;
            routineSetId = plankRoutineSet.Id;
            sessionId = session.Id;
            workoutSetId = plankWorkoutSet.Id;
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var verificationContext = await factory.CreateDbContextAsync();
        Assert.Null(await verificationContext.Exercises.SingleOrDefaultAsync(exercise =>
            exercise.Id == plankId));
        Assert.False(await verificationContext.Exercises.AnyAsync(exercise =>
            !exercise.IsCustom &&
            (exercise.NormalizedName == "PLANK" || exercise.NormalizedName == "PRANCHA")));

        var remainingRoutineExercise = await verificationContext.RoutineExercises
            .Include(exercise => exercise.Exercise)
            .SingleAsync(exercise => exercise.RoutineId == routineId);
        Assert.Equal("Deadlift", remainingRoutineExercise.Exercise.Name);
        Assert.Equal(0, remainingRoutineExercise.Position);
        Assert.Null(await verificationContext.RoutineSets.FindAsync(routineSetId));

        var historical = await verificationContext.WorkoutExercises
            .Include(exercise => exercise.Sets)
            .SingleAsync(exercise => exercise.WorkoutSessionId == sessionId);
        Assert.Null(historical.ExerciseId);
        Assert.Equal("Plank", historical.ExerciseName);
        var preservedSet = Assert.Single(historical.Sets);
        Assert.Equal(workoutSetId, preservedSet.Id);
        Assert.Equal(60, preservedSet.Repetitions);
        Assert.True(preservedSet.IsCompleted);
        Assert.Equal(48, await verificationContext.Exercises.CountAsync(exercise => !exercise.IsCustom));
    }

    [Fact]
    public async Task InitializeDatabase_WithCustomPlank_PreservesCustomExercise()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);

        int customPlankId;
        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();
            var customPlank = new Exercise
            {
                Name = "Plank",
                NormalizedName = "PLANK",
                MuscleGroup = MuscleGroup.Core,
                Equipment = Equipment.Bodyweight,
                IsCustom = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            setupContext.Exercises.Add(customPlank);
            await setupContext.SaveChangesAsync();
            customPlankId = customPlank.Id;
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var preserved = await verificationContext.Exercises.SingleAsync(exercise =>
            exercise.Id == customPlankId);
        Assert.True(preserved.IsCustom);
        Assert.Equal("Plank", preserved.Name);
        Assert.Equal(49, await verificationContext.Exercises.CountAsync());
    }

    [Fact]
    public async Task InitializeDatabase_FromPhaseTwoSchema_AddsRoutineTablesWithoutDeletingExercises()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                CREATE TABLE "Exercises" (
                    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "Name" TEXT COLLATE NOCASE NOT NULL,
                    "NormalizedName" TEXT NOT NULL,
                    "MuscleGroup" TEXT NOT NULL,
                    "Equipment" TEXT NOT NULL,
                    "Instructions" TEXT NULL,
                    "IsCustom" INTEGER NOT NULL,
                    "CreatedAt" INTEGER NOT NULL
                );
                CREATE UNIQUE INDEX "IX_Exercises_NormalizedName" ON "Exercises" ("NormalizedName");
                INSERT INTO "Exercises" ("Name", "NormalizedName", "MuscleGroup", "Equipment", "IsCustom", "CreatedAt")
                VALUES ('Exercício existente', 'EXERCÍCIO EXISTENTE', 'Back', 'Dumbbell', 1, 0);
                """;
            await command.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);
        var initializer = new DatabaseInitializer(factory);

        await initializer.InitializeAsync();

        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal(49, await context.Exercises.CountAsync());
        Assert.All(await context.Exercises.ToListAsync(), exercise => Assert.Null(exercise.ImagePath));
        Assert.Empty(await context.Routines.ToListAsync());
        Assert.Empty(await context.WorkoutSessions.ToListAsync());
    }

    [Fact]
    public async Task InitializeDatabase_WithPortugueseSeedData_TranslatesNamesWithoutChangingRelationships()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);
        var timestamp = new DateTimeOffset(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);

        int legacyExerciseId;
        int routineId;
        int sessionId;
        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();

            var legacyExercise = new Exercise
            {
                Name = "Agachamento com barra",
                NormalizedName = "AGACHAMENTO COM BARRA",
                MuscleGroup = MuscleGroup.Legs,
                Equipment = Equipment.Barbell,
                IsCustom = false,
                CreatedAt = timestamp
            };
            var routine = new Routine
            {
                Name = "Legacy routine",
                NormalizedName = "LEGACY ROUTINE",
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Exercises =
                [
                    new RoutineExercise { Exercise = legacyExercise, Position = 0 }
                ]
            };
            var session = new WorkoutSession
            {
                Routine = routine,
                RoutineName = routine.Name,
                StartedAt = timestamp,
                FinishedAt = timestamp.AddMinutes(30),
                Status = WorkoutStatus.Completed,
                Exercises =
                [
                    new WorkoutExercise
                    {
                        Exercise = legacyExercise,
                        ExerciseName = legacyExercise.Name,
                        Position = 0
                    }
                ]
            };

            setupContext.WorkoutSessions.Add(session);
            await setupContext.SaveChangesAsync();
            legacyExerciseId = legacyExercise.Id;
            routineId = routine.Id;
            sessionId = session.Id;
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var translated = await verificationContext.Exercises
            .SingleAsync(exercise => exercise.Id == legacyExerciseId);
        var routineExercise = await verificationContext.RoutineExercises
            .SingleAsync(item => item.RoutineId == routineId);
        var workoutExercise = await verificationContext.WorkoutExercises
            .SingleAsync(item => item.WorkoutSessionId == sessionId);

        Assert.Equal("Barbell squat", translated.Name);
        Assert.Equal("BARBELL SQUAT", translated.NormalizedName);
        Assert.Equal(legacyExerciseId, routineExercise.ExerciseId);
        Assert.Equal(legacyExerciseId, workoutExercise.ExerciseId);
        Assert.Equal("Barbell squat", workoutExercise.ExerciseName);
        Assert.Equal(48, await verificationContext.Exercises.CountAsync(exercise => !exercise.IsCustom));
    }

    [Fact]
    public async Task InitializeDatabase_WithCustomExerciseMatchingNewSeed_PreservesCustomExercise()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);

        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.Exercises.Add(new Exercise
            {
                Name = "Face Pull",
                NormalizedName = "FACE PULL",
                MuscleGroup = MuscleGroup.Shoulders,
                Equipment = Equipment.Cable,
                IsCustom = true,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await setupContext.SaveChangesAsync();
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var facePull = await verificationContext.Exercises
            .SingleAsync(exercise => exercise.NormalizedName == "FACE PULL");

        Assert.True(facePull.IsCustom);
        Assert.Equal(48, await verificationContext.Exercises.CountAsync());
    }

    [Fact]
    public async Task InitializeDatabase_WithPreviousEnglishSeed_RenamesAliasAndKeepsRelationships()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);
        var timestamp = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero);

        int exerciseId;
        int routineId;
        int sessionId;
        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();

            var previousSeed = new Exercise
            {
                Name = "Barbell row",
                NormalizedName = "BARBELL ROW",
                MuscleGroup = MuscleGroup.Back,
                Equipment = Equipment.Barbell,
                IsCustom = false,
                CreatedAt = timestamp
            };
            var routine = new Routine
            {
                Name = "Existing routine",
                NormalizedName = "EXISTING ROUTINE",
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Exercises =
                [
                    new RoutineExercise { Exercise = previousSeed, Position = 0 }
                ]
            };
            var session = new WorkoutSession
            {
                Routine = routine,
                RoutineName = routine.Name,
                StartedAt = timestamp,
                FinishedAt = timestamp.AddMinutes(30),
                Status = WorkoutStatus.Completed,
                Exercises =
                [
                    new WorkoutExercise
                    {
                        Exercise = previousSeed,
                        ExerciseName = previousSeed.Name,
                        Position = 0
                    }
                ]
            };

            setupContext.WorkoutSessions.Add(session);
            await setupContext.SaveChangesAsync();
            exerciseId = previousSeed.Id;
            routineId = routine.Id;
            sessionId = session.Id;
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var renamed = await verificationContext.Exercises.SingleAsync(item => item.Id == exerciseId);
        var routineExercise = await verificationContext.RoutineExercises
            .SingleAsync(item => item.RoutineId == routineId);
        var workoutExercise = await verificationContext.WorkoutExercises
            .SingleAsync(item => item.WorkoutSessionId == sessionId);

        Assert.Equal("Bent Over Row", renamed.Name);
        Assert.Equal("BENT OVER ROW", renamed.NormalizedName);
        Assert.Equal(exerciseId, routineExercise.ExerciseId);
        Assert.Equal(exerciseId, workoutExercise.ExerciseId);
        Assert.Equal("Bent Over Row", workoutExercise.ExerciseName);
        Assert.Equal(48, await verificationContext.Exercises.CountAsync(exercise => !exercise.IsCustom));
    }

    [Fact]
    public async Task InitializeDatabase_WithRenamedHevyExercises_UpdatesNamesAndEquipment()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);

        int rowId;
        int curlId;
        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();

            var row = new Exercise
            {
                Name = "Chest Supported Incline Row (Dumbbell)",
                NormalizedName = "CHEST SUPPORTED INCLINE ROW (DUMBBELL)",
                MuscleGroup = MuscleGroup.Back,
                Equipment = Equipment.Dumbbell,
                IsCustom = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var curl = new Exercise
            {
                Name = "Behind the Back Curl (Cable)",
                NormalizedName = "BEHIND THE BACK CURL (CABLE)",
                MuscleGroup = MuscleGroup.Biceps,
                Equipment = Equipment.Cable,
                IsCustom = false,
                CreatedAt = DateTimeOffset.UtcNow
            };
            setupContext.Exercises.AddRange(row, curl);
            await setupContext.SaveChangesAsync();
            rowId = row.Id;
            curlId = curl.Id;
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var migratedRow = await verificationContext.Exercises.SingleAsync(exercise => exercise.Id == rowId);
        var migratedCurl = await verificationContext.Exercises.SingleAsync(exercise => exercise.Id == curlId);

        Assert.Equal("Chest Supported Row / T-Bar Row", migratedRow.Name);
        Assert.Equal(Equipment.Machine, migratedRow.Equipment);
        Assert.False(await verificationContext.Exercises.AnyAsync(exercise =>
            exercise.Name == "Chest Supported Incline Row (Dumbbell)"));
        Assert.Equal("Bayesian Curl (Cable)", migratedCurl.Name);
        Assert.Equal(Equipment.Cable, migratedCurl.Equipment);
        Assert.Equal(48, await verificationContext.Exercises.CountAsync(exercise => !exercise.IsCustom));
    }

    [Fact]
    public async Task InitializeDatabase_WithPreviousChestSupportedRowName_PreservesIdAndRelationships()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);
        var timestamp = new DateTimeOffset(2026, 7, 17, 12, 0, 0, TimeSpan.Zero);

        int exerciseId;
        int routineId;
        int sessionId;
        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();

            var previousExercise = new Exercise
            {
                Name = "Chest Supported T-Bar Row (Machine)",
                NormalizedName = "CHEST SUPPORTED T-BAR ROW (MACHINE)",
                MuscleGroup = MuscleGroup.Back,
                Equipment = Equipment.Machine,
                IsCustom = false,
                CreatedAt = timestamp
            };
            var routine = new Routine
            {
                Name = "Back day",
                NormalizedName = "BACK DAY",
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Exercises =
                [
                    new RoutineExercise { Exercise = previousExercise, Position = 0 }
                ]
            };
            var session = new WorkoutSession
            {
                Routine = routine,
                RoutineName = routine.Name,
                StartedAt = timestamp,
                FinishedAt = timestamp.AddMinutes(40),
                Status = WorkoutStatus.Completed,
                Exercises =
                [
                    new WorkoutExercise
                    {
                        Exercise = previousExercise,
                        ExerciseName = previousExercise.Name,
                        Position = 0
                    }
                ]
            };

            setupContext.WorkoutSessions.Add(session);
            await setupContext.SaveChangesAsync();
            exerciseId = previousExercise.Id;
            routineId = routine.Id;
            sessionId = session.Id;
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var migrated = await verificationContext.Exercises.SingleAsync(item => item.Id == exerciseId);
        var routineExercise = await verificationContext.RoutineExercises
            .SingleAsync(item => item.RoutineId == routineId);
        var workoutExercise = await verificationContext.WorkoutExercises
            .SingleAsync(item => item.WorkoutSessionId == sessionId);

        Assert.Equal("Chest Supported Row / T-Bar Row", migrated.Name);
        Assert.Equal("CHEST SUPPORTED ROW / T-BAR ROW", migrated.NormalizedName);
        Assert.Equal(exerciseId, routineExercise.ExerciseId);
        Assert.Equal(exerciseId, workoutExercise.ExerciseId);
        Assert.Equal("Chest Supported Row / T-Bar Row", workoutExercise.ExerciseName);
        Assert.Equal(48, await verificationContext.Exercises.CountAsync(exercise => !exercise.IsCustom));
    }

    [Fact]
    public async Task InitializeDatabase_WithPortugueseWideRow_RenamesInPlaceAndKeepsRelationships()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);
        var timestamp = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);

        int exerciseId;
        int routineId;
        int sessionId;
        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();

            var legacyExercise = new Exercise
            {
                Name = "Remada Aberta Maquina",
                NormalizedName = "REMADA ABERTA MAQUINA",
                MuscleGroup = MuscleGroup.Back,
                Equipment = Equipment.Machine,
                IsCustom = false,
                CreatedAt = timestamp
            };
            var routine = new Routine
            {
                Name = "Back routine",
                NormalizedName = "BACK ROUTINE",
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Exercises =
                [
                    new RoutineExercise { Exercise = legacyExercise, Position = 0 }
                ]
            };
            var session = new WorkoutSession
            {
                Routine = routine,
                RoutineName = routine.Name,
                StartedAt = timestamp,
                FinishedAt = timestamp.AddMinutes(30),
                Status = WorkoutStatus.Completed,
                Exercises =
                [
                    new WorkoutExercise
                    {
                        Exercise = legacyExercise,
                        ExerciseName = legacyExercise.Name,
                        Position = 0
                    }
                ]
            };

            setupContext.WorkoutSessions.Add(session);
            await setupContext.SaveChangesAsync();
            exerciseId = legacyExercise.Id;
            routineId = routine.Id;
            sessionId = session.Id;
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var verificationContext = await factory.CreateDbContextAsync();
        var renamed = await verificationContext.Exercises.SingleAsync(item => item.Id == exerciseId);
        var routineExercise = await verificationContext.RoutineExercises
            .SingleAsync(item => item.RoutineId == routineId);
        var workoutExercise = await verificationContext.WorkoutExercises
            .SingleAsync(item => item.WorkoutSessionId == sessionId);

        Assert.Equal("Wide Row (Machine)", renamed.Name);
        Assert.Equal("WIDE ROW (MACHINE)", renamed.NormalizedName);
        Assert.Equal(exerciseId, routineExercise.ExerciseId);
        Assert.Equal(exerciseId, workoutExercise.ExerciseId);
        Assert.Equal("Wide Row (Machine)", workoutExercise.ExerciseName);
        Assert.Equal(48, await verificationContext.Exercises.CountAsync(exercise => !exercise.IsCustom));
    }

    [Fact]
    public async Task InitializeDatabase_WithDuplicateShoulderPresses_ConsolidatesRelationshipsAndSets()
    {
        SQLitePCL.Batteries.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;
        var factory = new TestContextFactory(options);
        var timestamp = new DateTimeOffset(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);

        int canonicalId;
        int duplicateId;
        int routineId;
        int sessionId;
        await using (var setupContext = await factory.CreateDbContextAsync())
        {
            await setupContext.Database.EnsureCreatedAsync();

            var canonical = new Exercise
            {
                Name = "Shoulder Press (Dumbbell)",
                NormalizedName = "SHOULDER PRESS (DUMBBELL)",
                MuscleGroup = MuscleGroup.Shoulders,
                Equipment = Equipment.Dumbbell,
                IsCustom = false,
                CreatedAt = timestamp
            };
            var duplicate = new Exercise
            {
                Name = "Seated Overhead Press (Dumbbell)",
                NormalizedName = "SEATED OVERHEAD PRESS (DUMBBELL)",
                MuscleGroup = MuscleGroup.Shoulders,
                Equipment = Equipment.Dumbbell,
                IsCustom = false,
                CreatedAt = timestamp
            };
            var deadlift = new Exercise
            {
                Name = "Deadlift",
                NormalizedName = "DEADLIFT",
                MuscleGroup = MuscleGroup.Back,
                Equipment = Equipment.Barbell,
                IsCustom = false,
                CreatedAt = timestamp
            };
            var routine = new Routine
            {
                Name = "Shoulders",
                NormalizedName = "SHOULDERS",
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Exercises =
                [
                    new RoutineExercise
                    {
                        Exercise = duplicate,
                        Position = 0,
                        Sets =
                        [
                            new RoutineSet { SetNumber = 1, Weight = 10, Repetitions = 12 },
                            new RoutineSet { SetNumber = 2, Weight = 12, Repetitions = 10 }
                        ]
                    },
                    new RoutineExercise { Exercise = deadlift, Position = 1 },
                    new RoutineExercise
                    {
                        Exercise = canonical,
                        Position = 2,
                        Sets =
                        [
                            new RoutineSet { SetNumber = 1, Weight = 20, Repetitions = 8 }
                        ]
                    }
                ]
            };
            var session = new WorkoutSession
            {
                Routine = routine,
                RoutineName = routine.Name,
                StartedAt = timestamp,
                FinishedAt = timestamp.AddMinutes(45),
                Status = WorkoutStatus.Completed,
                Exercises =
                [
                    new WorkoutExercise
                    {
                        Exercise = duplicate,
                        ExerciseName = duplicate.Name,
                        Position = 0,
                        Sets =
                        [
                            new WorkoutSet
                            {
                                SetNumber = 1,
                                Weight = 10,
                                Repetitions = 12,
                                IsCompleted = true,
                                CompletedAt = timestamp.AddMinutes(5)
                            }
                        ]
                    },
                    new WorkoutExercise
                    {
                        Exercise = canonical,
                        ExerciseName = canonical.Name,
                        Position = 1,
                        Sets =
                        [
                            new WorkoutSet
                            {
                                SetNumber = 1,
                                Weight = 20,
                                Repetitions = 8,
                                IsCompleted = true,
                                CompletedAt = timestamp.AddMinutes(10)
                            }
                        ]
                    }
                ]
            };

            setupContext.WorkoutSessions.Add(session);
            await setupContext.SaveChangesAsync();
            canonicalId = canonical.Id;
            duplicateId = duplicate.Id;
            routineId = routine.Id;
            sessionId = session.Id;
        }

        var initializer = new DatabaseInitializer(factory);
        await initializer.InitializeAsync();

        await using var verificationContext = await factory.CreateDbContextAsync();
        Assert.NotNull(await verificationContext.Exercises.FindAsync(canonicalId));
        Assert.Null(await verificationContext.Exercises.FindAsync(duplicateId));

        var routineExercises = await verificationContext.RoutineExercises
            .Include(item => item.Sets)
            .Where(item => item.RoutineId == routineId)
            .OrderBy(item => item.Position)
            .ToListAsync();
        Assert.Equal([0, 1], routineExercises.Select(item => item.Position));

        var shoulderPress = Assert.Single(
            routineExercises,
            item => item.ExerciseId == canonicalId);
        var mergedSets = shoulderPress.Sets.OrderBy(item => item.SetNumber).ToArray();
        Assert.Equal([1, 2, 3], mergedSets.Select(item => item.SetNumber));
        Assert.Equal([20m, 10m, 12m], mergedSets.Select(item => item.Weight));

        var workoutExercises = await verificationContext.WorkoutExercises
            .Include(item => item.Sets)
            .Where(item => item.WorkoutSessionId == sessionId)
            .OrderBy(item => item.Position)
            .ToListAsync();
        Assert.Equal(2, workoutExercises.Count);
        Assert.All(workoutExercises, item =>
        {
            Assert.Equal(canonicalId, item.ExerciseId);
            Assert.Equal("Shoulder Press (Dumbbell)", item.ExerciseName);
            Assert.Single(item.Sets);
        });
        Assert.Equal(48, await verificationContext.Exercises.CountAsync(exercise => !exercise.IsCustom));
    }

    private sealed class TestContextFactory(DbContextOptions<LiftLogDbContext> options)
        : IDbContextFactory<LiftLogDbContext>
    {
        public LiftLogDbContext CreateDbContext() => new(options);

        public Task<LiftLogDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
