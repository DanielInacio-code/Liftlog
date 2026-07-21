using LiftLog.Core.Models;

namespace LiftLog.App.Services;

public static class ExerciseImageSource
{
    public const string DefaultCustomImage = "exercise_custom.svg";

    private static readonly IReadOnlyDictionary<string, string> BuiltInImages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Barbell bench press"] = "exercise_barbell_bench_press.png",
            ["Dumbbell bench press"] = "exercise_dumbbell_bench_press.png",
            ["Barbell squat"] = "exercise_barbell_squat.png",
            ["Deadlift"] = "exercise_deadlift.png",
            ["Barbell row"] = "exercise_bent_over_row.png",
            ["Lat pulldown"] = "exercise_lat_pulldown.png",
            ["Dumbbell shoulder press"] = "exercise_shoulder_press.png",
            ["Dumbbell lateral raise"] = "exercise_lateral_raise.png",
            ["Dumbbell biceps curl"] = "exercise_biceps_curl.png",
            ["Leg press"] = "exercise_leg_press.png",
            ["Leg extension"] = "exercise_leg_extension.png",
            ["Leg curl"] = "exercise_lying_leg_curl.png",
            ["Calf raise"] = "exercise_calf_raise.png",
            ["Triceps Pushdown"] = "exercise_triceps_pushdown.png",
            ["Single-Arm Triceps Pushdown"] = "exercise_single_arm_triceps_pushdown.png",
            ["Preacher Curl (Dumbbell)"] = "exercise_preacher_curl_dumbbell.png",
            ["Lying Leg Curl"] = "exercise_lying_leg_curl.png",
            ["Seated Leg Curl"] = "exercise_seated_leg_curl.png",
            ["Seated Leg Curl (Machine)"] = "exercise_seated_leg_curl.png",
            ["Calf Extension (Machine)"] = "exercise_calf_raise.png",
            ["Leg Extension (Machine)"] = "exercise_leg_extension.png",
            ["Chest Press (Machine)"] = "exercise_chest_press_machine.png",
            ["Chest Press - Neutral Grip (Machine)"] = "exercise_chest_press_neutral_grip_machine.png",
            ["Bent Over Row"] = "exercise_bent_over_row.png",
            ["Bent Over Row (Barbell)"] = "exercise_bent_over_row.png",
            ["Chest Fly (Machine)"] = "exercise_chest_fly_machine.png",
            ["Lat Pulldown (Cable)"] = "exercise_lat_pulldown.png",
            ["Chest Dip"] = "exercise_chest_dip.png",
            ["Lateral Raise (Machine)"] = "exercise_lateral_raise_machine.png",
            ["Seated Incline Curl (Dumbbell)"] = "exercise_seated_incline_curl_dumbbell.png",
            ["Hammer Curl (Dumbbell)"] = "exercise_hammer_curl_dumbbell.png",
            ["Leg Press (Machine)"] = "exercise_leg_press.png",
            ["Straight Leg Deadlift"] = "exercise_straight_leg_deadlift.png",
            ["Chest Supported Row / T-Bar Row"] = "exercise_chest_supported_t_bar_row_machine.png",
            ["Chest Supported T-Bar Row (Machine)"] = "exercise_chest_supported_t_bar_row_machine.png",
            ["Shoulder Press (Dumbbell)"] = "exercise_shoulder_press.png",
            ["Pullover (Machine)"] = "exercise_pullover_machine.png",
            ["Straight-Arm Pulldown"] = "exercise_straight_arm_pulldown.png",
            ["Single Arm Cable Row"] = "exercise_single_arm_cable_row.png",
            ["Lateral Raise (Cable)"] = "exercise_lateral_raise_cable.png",
            ["Single Leg Press (Machine)"] = "exercise_single_leg_press_machine.png",
            ["Overhead Triceps Extension (Cable)"] = "exercise_overhead_triceps_extension_cable.png",
            ["Hip Adduction (Machine)"] = "exercise_hip_adduction_machine.png",
            ["Preacher Curl (Machine)"] = "exercise_preacher_curl_machine.png",
            ["Wide Row (Machine)"] = "exercise_wide_row_machine.png",
            ["Remada Aberta Maquina"] = "exercise_wide_row_machine.png",
            ["Lat Pulldown - Close Grip (Cable)"] = "exercise_lat_pulldown_close_grip_cable.png",
            ["Lateral Raise (Dumbbell)"] = "exercise_lateral_raise.png",
            ["Shoulder Press (Machine)"] = "exercise_shoulder_press_machine.png",
            ["Seated Shoulder Press (Machine)"] = "exercise_shoulder_press_machine.png",
            ["Butterfly (Pec Deck)"] = "exercise_butterfly_pec_deck.png",
            ["Bayesian Curl (Cable)"] = "exercise_bayesian_curl_cable.png",
            ["Behind the Back Curl (Cable)"] = "exercise_bayesian_curl_cable.png",
            ["Hip Abduction (Machine)"] = "exercise_hip_abduction_machine.png",
            ["Incline Bench Press (Dumbbell)"] = "exercise_incline_bench_press_dumbbell.png",
            ["Decline Bench Press (Machine)"] = "exercise_decline_bench_press_machine.png",
            ["Rear Delt Reverse Fly (Cable)"] = "exercise_rear_delt_reverse_fly_cable.png",
            ["Preacher Curl (Barbell)"] = "exercise_preacher_curl_barbell.png",
            ["Hack Squat (Machine)"] = "exercise_hack_squat_machine.png",
            ["Seated Overhead Press (Dumbbell)"] = "exercise_shoulder_press.png",
            ["Seated Cable Row - V Grip (Cable)"] = "exercise_seated_cable_row_v_grip.png",
            ["Face Pull"] = "exercise_face_pull.png",
            ["Romanian Deadlift (Barbell)"] = "exercise_romanian_deadlift_barbell.png"
        };

    public static string For(Exercise exercise)
    {
        if (!string.IsNullOrWhiteSpace(exercise.ImagePath) && File.Exists(exercise.ImagePath))
        {
            return exercise.ImagePath;
        }

        return !exercise.IsCustom && BuiltInImages.TryGetValue(exercise.Name, out var image)
            ? image
            : DefaultCustomImage;
    }

    public static string ForThumbnail(Exercise exercise)
    {
        if (!string.IsNullOrWhiteSpace(exercise.ImagePath) && File.Exists(exercise.ImagePath))
        {
            var thumbnailPath = ExerciseImagePaths.GetThumbnailPath(exercise.ImagePath);
            return File.Exists(thumbnailPath) ? thumbnailPath : exercise.ImagePath;
        }

        return !exercise.IsCustom && BuiltInImages.TryGetValue(exercise.Name, out var image)
            ? image
            : DefaultCustomImage;
    }

    public static string ForName(string exerciseName) =>
        BuiltInImages.TryGetValue(exerciseName, out var image)
            ? image
            : DefaultCustomImage;
}
