using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GelerIK.Runtime.Solvers;

namespace GelerIK.Tests.EditMode
{
    internal sealed class IKBenchmarkConfig
    {
        public int samplesPerCategory = 200;
        public int warmupSamplesPerSolver = 1;
        public int fixedMaxIterations = 500;
        public float positionTolerance = 0.1f;
        public int randomSeed = 20260406;
        public float[] boneLengths = { 1.0f, 0.9f, 0.75f, 0.55f };
        public float reachableMinRadiusRatio = 0.15f;
        public float reachableMaxRadiusRatio = 0.95f;
        public float unreachableMinRadiusRatio = 1.05f;
        public float unreachableMaxRadiusRatio = 1.35f;
        public float[] stepScaleSweep = { 0.0001f, 0.0002f, 0.0005f, 0.0010f, 0.0020f, 0.0050f, 0.0100f, 0.0200f, 0.0500f };
    }

    internal struct IKBenchmarkCategory
    {
        public string name;
        public bool reachable;
        public float minRadiusRatio;
        public float maxRadiusRatio;

        public IKBenchmarkCategory(string name, bool reachable, float minRadiusRatio, float maxRadiusRatio)
        {
            this.name = name;
            this.reachable = reachable;
            this.minRadiusRatio = minRadiusRatio;
            this.maxRadiusRatio = maxRadiusRatio;
        }
    }

    internal struct IKSolverRegistration
    {
        public string seriesName;
        public string methodFamily;
        public string tuningName;
        public float tuningValue;
        public Func<IIKSolver> createSolver;

        public IKSolverRegistration(
            string seriesName,
            string methodFamily,
            string tuningName,
            float tuningValue,
            Func<IIKSolver> createSolver)
        {
            this.seriesName = seriesName;
            this.methodFamily = methodFamily;
            this.tuningName = tuningName;
            this.tuningValue = tuningValue;
            this.createSolver = createSolver;
        }
    }

    internal struct IKStepSweepPoint
    {
        public float stepScale;

        public IKStepSweepPoint(float stepScale)
        {
            this.stepScale = stepScale;
        }
    }

    internal sealed class IKBenchmarkSampleDefinition
    {
        public int sampleId;
        public string categoryName;
        public bool reachableCategory;
        public float radiusRatio;
        public float targetDistance;
        public UnityEngine.Vector3 targetPosition;
    }

    internal sealed class IKBenchmarkSampleResult
    {
        public int sampleId;
        public string categoryName;
        public bool reachableCategory;
        public string seriesName;
        public string methodFamily;
        public string tuningName;
        public float tuningValue;
        public float stepScale;
        public bool converged;
        public int iterations;
        public float elapsedMilliseconds;
        public float finalPositionError;
        public float jointRotationDeviationDegrees;
    }

    internal sealed class IKBenchmarkStepPoint
    {
        public string categoryName;
        public bool reachableCategory;
        public string seriesName;
        public string methodFamily;
        public string tuningName;
        public float tuningValue;
        public float stepScale;
        public int sampleCount;
        public int successCount;
        public float successRate;
        public float averageConvergedIterations;
        public float averageRuntimeAllSamples;
        public float averageFinalError;
        public float averageJointRotationDeviationDegrees;
    }

    internal sealed class IKBenchmarkScorePoint
    {
        public string seriesName;
        public string methodFamily;
        public string tuningName;
        public float tuningValue;
        public float stepScale;
        public float compositeScore;
        public float reachableSuccessRate;
        public float reachableAverageConvergedIterations;
        public float reachableAverageRuntimeAllSamples;
        public float reachableAverageFinalError;
        public float reachableAverageJointRotationDeviationDegrees;
        public float unreachableAverageRuntimeAllSamples;
        public float unreachableAverageFinalError;
        public float unreachableAverageJointRotationDeviationDegrees;
    }

    internal sealed class IKBenchmarkBestScorePoint
    {
        public string seriesName;
        public string methodFamily;
        public string tuningName;
        public float tuningValue;
        public float bestStepScale;
        public float compositeScore;
    }

    internal sealed class IKBenchmarkReport
    {
        public IKBenchmarkConfig config;
        public List<IKBenchmarkCategory> categories = new();
        public List<IKSolverRegistration> solverRegistrations = new();
        public List<IKStepSweepPoint> stepSweepPoints = new();
        public List<IKBenchmarkSampleDefinition> sampleDefinitions = new();
        public List<IKBenchmarkSampleResult> sampleResults = new();
        public List<IKBenchmarkStepPoint> stepPoints = new();
        public List<IKBenchmarkScorePoint> scorePoints = new();
        public List<IKBenchmarkBestScorePoint> bestScorePoints = new();
        public float totalReach;

        public string BuildExperimentPlanText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("IK Benchmark Experiment Plan");
            builder.AppendLine();
            builder.AppendLine("Goal");
            builder.AppendLine("Compare CCD, Jacobian Transpose, fixed-damping DLS, and SVD-adaptive DLS under a one-dimensional step-scale sweep.");
            builder.AppendLine();
            builder.AppendLine("Fixed Solve Settings");
            builder.AppendLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "maxIterations={0}, positionTolerance={1:0.###}, warmupSamplesPerSolver={2}, totalReach={3:0.###}",
                    config.fixedMaxIterations,
                    config.positionTolerance,
                    config.warmupSamplesPerSolver,
                    totalReach));
            builder.AppendLine();
            builder.AppendLine("Sweep Axis");
            builder.AppendLine("step_scale");
            builder.AppendLine("stepScaleSweep=" + JoinFloats(config.stepScaleSweep));
            builder.AppendLine();
            builder.AppendLine("Sample Design");
            builder.AppendLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Reachable: {0} samples, ratio range [{1:0.###}, {2:0.###}]",
                    config.samplesPerCategory,
                    config.reachableMinRadiusRatio,
                    config.reachableMaxRadiusRatio));
            builder.AppendLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Unreachable: {0} samples, ratio range [{1:0.###}, {2:0.###}]",
                    config.samplesPerCategory,
                    config.unreachableMinRadiusRatio,
                    config.unreachableMaxRadiusRatio));
            builder.AppendLine(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Samples are fixed by random seed {0} and reused for every solver and every step scale.",
                    config.randomSeed));
            builder.AppendLine();
            builder.AppendLine("Rotation Deviation Metric");
            builder.AppendLine("Average per-joint local-rotation offset from the initial pose, measured in degrees at solve end.");
            builder.AppendLine();
            builder.AppendLine("Solver Series");

            for (int i = 0; i < solverRegistrations.Count; i++)
            {
                IKSolverRegistration solver = solverRegistrations[i];
                builder.AppendLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: family={1}, {2}={3:0.######}",
                        solver.seriesName,
                        solver.methodFamily,
                        solver.tuningName,
                        solver.tuningValue));
            }

            return builder.ToString();
        }

        public string BuildScoreFormulaText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("IK Benchmark Composite Score");
            builder.AppendLine();
            builder.AppendLine("Normalization");
            builder.AppendLine("For higher-is-better metrics: N_high(x) = (x - x_min) / (x_max - x_min + eps)");
            builder.AppendLine("For lower-is-better metrics: N_low(x) = (x_max - x) / (x_max - x_min + eps)");
            builder.AppendLine("The min and max are computed over all tested (solver, step_scale) points in this benchmark.");
            builder.AppendLine();
            builder.AppendLine("Score Formula");
            builder.AppendLine("Score = 100 * (");
            builder.AppendLine("  0.20 * N_high(reachable_success_rate)");
            builder.AppendLine("  + 0.10 * N_low(reachable_avg_success_iterations)");
            builder.AppendLine("  + 0.10 * N_low(reachable_avg_runtime_ms)");
            builder.AppendLine("  + 0.10 * N_low(reachable_avg_final_error)");
            builder.AppendLine("  + 0.20 * N_low(reachable_avg_joint_rotation_deviation_deg)");
            builder.AppendLine("  + 0.05 * N_low(unreachable_avg_runtime_ms)");
            builder.AppendLine("  + 0.15 * N_low(unreachable_avg_final_error)");
            builder.AppendLine("  + 0.10 * N_low(unreachable_avg_joint_rotation_deviation_deg)");
            builder.AppendLine(")");
            builder.AppendLine();
            builder.AppendLine("Interpretation");
            builder.AppendLine("Higher scores are better.");
            builder.AppendLine("The final bar chart uses the best score over the tested step-scale range for each solver.");
            return builder.ToString();
        }

        public string BuildSummaryText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("IK Benchmark Summary");

            for (int i = 0; i < bestScorePoints.Count; i++)
            {
                IKBenchmarkBestScorePoint point = bestScorePoints[i];
                builder.AppendLine();
                builder.AppendLine(point.seriesName);
                builder.AppendLine(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "best_score={0:0.###}, best_step_scale={1:0.######}",
                        point.compositeScore,
                        point.bestStepScale));
            }

            return builder.ToString();
        }

        private static string JoinFloats(IReadOnlyList<float> values)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(values[i].ToString("0.######", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
