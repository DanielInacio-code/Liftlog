using LiftLog.Core.Data;
using LiftLog.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LiftLog.Tests.Support;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<LiftLogDbContext> _contextFactory;

    private SqliteTestDatabase(
        SqliteConnection connection,
        IDbContextFactory<LiftLogDbContext> contextFactory,
        IExerciseService exerciseService,
        IRoutineService routineService,
        IWorkoutService workoutService,
        IHistoryService historyService,
        IStatisticsService statisticsService,
        IBodyMeasurementService bodyMeasurementService)
    {
        _connection = connection;
        _contextFactory = contextFactory;
        ExerciseService = exerciseService;
        RoutineService = routineService;
        WorkoutService = workoutService;
        HistoryService = historyService;
        StatisticsService = statisticsService;
        BodyMeasurementService = bodyMeasurementService;
    }

    public IExerciseService ExerciseService { get; }

    public IRoutineService RoutineService { get; }

    public IWorkoutService WorkoutService { get; }

    public IHistoryService HistoryService { get; }

    public IStatisticsService StatisticsService { get; }

    public IBodyMeasurementService BodyMeasurementService { get; }

    public Task<LiftLogDbContext> CreateDbContextAsync(
        CancellationToken cancellationToken = default) =>
        _contextFactory.CreateDbContextAsync(cancellationToken);

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        SQLitePCL.Batteries.Init();

        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LiftLogDbContext>()
            .UseSqlite(connection)
            .Options;

        var factory = new TestContextFactory(options);
        var initializer = new DatabaseInitializer(factory);
        var exerciseService = new ExerciseService(factory, initializer);
        var routineService = new RoutineService(factory, initializer);
        var workoutService = new WorkoutService(factory, initializer);
        var historyService = new HistoryService(factory, initializer);
        var statisticsService = new StatisticsService(factory, initializer);
        var bodyMeasurementService = new BodyMeasurementService(factory, initializer);

        return new SqliteTestDatabase(
            connection,
            factory,
            exerciseService,
            routineService,
            workoutService,
            historyService,
            statisticsService,
            bodyMeasurementService);
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    private sealed class TestContextFactory(DbContextOptions<LiftLogDbContext> options)
        : IDbContextFactory<LiftLogDbContext>
    {
        public LiftLogDbContext CreateDbContext() => new(options);

        public Task<LiftLogDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
