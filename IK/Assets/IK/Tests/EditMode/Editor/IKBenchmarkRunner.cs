using System;
using System.Collections.Generic;
using System.Diagnostics;
using GelerIK.Runtime.Core;
using GelerIK.Runtime.Model;
using GelerIK.Runtime.Solvers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GelerIK.Tests.EditMode
{
    internal static class IKBenchmarkRunner
    {
        public static IKBenchmarkReport Run(
            IKBenchmarkConfig config,
            IList<IKBenchmarkCategory> categories,
            IList<IKSolverRegistration> solverRegistrations)
        {
            ValidateConfig(config);

            IKBenchmarkReport report = new IKBenchmarkReport
            {
                config = config
            };

            report.categories.AddRange(categories);
            report.solverRegistrations.AddRange(solverRegistrations);

            using (BenchmarkChainFixture fixture = BenchmarkChainFixture.Create(config.boneLengths))
            {
                report.totalReach = fixture.TotalReach;
                report.sampleDefinitions.AddRange(BuildSampleDefinitions(config, categories, fixture.TotalReach));
                report.stepSweepPoints.AddRange(BuildSweepPoints(config));

                for (int solverIndex = 0; solverIndex < solverRegistrations.Count; solverIndex++)
                {
                    IKSolverRegistration solverRegistration = solverRegistrations[solverIndex];

                    for (int sweepIndex = 0; sweepIndex < report.stepSweepPoints.Count; sweepIndex++)
                    {
                        IKStepSweepPoint sweepPoint = report.stepSweepPoints[sweepIndex];
                        IIKSolver solver = solverRegistration.createSolver();
                        WarmupSolver(config, fixture, solverRegistration, sweepPoint, solver);
                        RunAllSamples(
                            config,
                            fixture,
                            solverRegistration,
                            sweepPoint,
                            solver,
                            report.sampleDefinitions,
                            report.sampleResults);
                    }
                }
            }

            report.stepPoints.AddRange(BuildStepPoints(report));
            SortStepPoints(report.stepPoints);
            report.scorePoints.AddRange(BuildScorePoints(report));
            SortScorePoints(report.scorePoints);
            report.bestScorePoints.AddRange(BuildBestScorePoints(report.scorePoints));
            SortBestScorePoints(report.bestScorePoints);
            return report;
        }

        private static void ValidateConfig(IKBenchmarkConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (config.samplesPerCategory <= 0)
            {
                throw new InvalidOperationException("samplesPerCategory must be positive.");
            }

            if (config.fixedMaxIterations <= 0)
            {
                throw new InvalidOperationException("fixedMaxIterations must be positive.");
            }

            if (config.stepScaleSweep == null || config.stepScaleSweep.Length == 0)
            {
                throw new InvalidOperationException("stepScaleSweep must contain at least one value.");
            }

            if (config.boneLengths == null || config.boneLengths.Length < 2)
            {
                throw new InvalidOperationException("boneLengths must describe at least a two-bone chain.");
            }
        }

        private static List<IKBenchmarkSampleDefinition> BuildSampleDefinitions(
            IKBenchmarkConfig config,
            IList<IKBenchmarkCategory> categories,
            float totalReach)
        {
            List<IKBenchmarkSampleDefinition> samples = new List<IKBenchmarkSampleDefinition>(
                categories.Count * config.samplesPerCategory);

            int sampleId = 0;

            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                IKBenchmarkCategory category = categories[categoryIndex];
                System.Random random = new System.Random(config.randomSeed + categoryIndex * 1009);

                for (int sampleIndex = 0; sampleIndex < config.samplesPerCategory; sampleIndex++)
                {
                    float radiusRatio = Mathf.Lerp(category.minRadiusRatio, category.maxRadiusRatio, NextFloat(random));
                    Vector3 targetPosition = SampleDirection(random) * (radiusRatio * totalReach);

                    samples.Add(new IKBenchmarkSampleDefinition
                    {
                        sampleId = sampleId++,
                        categoryName = category.name,
                        reachableCategory = category.reachable,
                        radiusRatio = radiusRatio,
                        targetDistance = targetPosition.magnitude,
                        targetPosition = targetPosition
                    });
                }
            }

            return samples;
        }

        private static List<IKStepSweepPoint> BuildSweepPoints(IKBenchmarkConfig config)
        {
            List<IKStepSweepPoint> sweepPoints = new List<IKStepSweepPoint>(config.stepScaleSweep.Length);

            for (int i = 0; i < config.stepScaleSweep.Length; i++)
            {
                sweepPoints.Add(new IKStepSweepPoint(config.stepScaleSweep[i]));
            }

            return sweepPoints;
        }

        private static void WarmupSolver(
            IKBenchmarkConfig config,
            BenchmarkChainFixture fixture,
            IKSolverRegistration solverRegistration,
            IKStepSweepPoint sweepPoint,
            IIKSolver solver)
        {
            System.Random random = new System.Random(
                config.randomSeed +
                StableHash(solverRegistration.seriesName) +
                Mathf.RoundToInt(sweepPoint.stepScale * 1000000f));

            for (int i = 0; i < config.warmupSamplesPerSolver; i++)
            {
                float radiusRatio = Mathf.Lerp(0.25f, 1.15f, NextFloat(random));
                Vector3 targetPosition = SampleDirection(random) * (radiusRatio * fixture.TotalReach);
                IKSolveRequest request = CreateRequest(config, fixture, sweepPoint, targetPosition);
                solver.Solve(request);
            }
        }

        private static void RunAllSamples(
            IKBenchmarkConfig config,
            BenchmarkChainFixture fixture,
            IKSolverRegistration solverRegistration,
            IKStepSweepPoint sweepPoint,
            IIKSolver solver,
            IReadOnlyList<IKBenchmarkSampleDefinition> samples,
            List<IKBenchmarkSampleResult> sampleResults)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                IKBenchmarkSampleDefinition sample = samples[i];
                IKSolveRequest request = CreateRequest(config, fixture, sweepPoint, sample.targetPosition);

                Stopwatch stopwatch = Stopwatch.StartNew();
                IKSolveResult solveResult = solver.Solve(request);
                stopwatch.Stop();

                sampleResults.Add(new IKBenchmarkSampleResult
                {
                    sampleId = sample.sampleId,
                    categoryName = sample.categoryName,
                    reachableCategory = sample.reachableCategory,
                    seriesName = solverRegistration.seriesName,
                    methodFamily = solverRegistration.methodFamily,
                    tuningName = solverRegistration.tuningName,
                    tuningValue = solverRegistration.tuningValue,
                    stepScale = sweepPoint.stepScale,
                    converged = solveResult != null && solveResult.converged,
                    iterations = solveResult != null ? solveResult.iterations : 0,
                    elapsedMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds,
                    finalPositionError = solveResult != null ? solveResult.finalPositionError : float.PositiveInfinity,
                    jointRotationDeviationDegrees = ComputeJointRotationDeviationDegrees(fixture.InitialState, request.state)
                });
            }
        }

        private static IKSolveRequest CreateRequest(
            IKBenchmarkConfig config,
            BenchmarkChainFixture fixture,
            IKStepSweepPoint sweepPoint,
            Vector3 targetPosition)
        {
            return new IKSolveRequest
            {
                definition = fixture.Definition,
                state = CloneState(fixture.InitialState),
                targetPosition = targetPosition,
                targetRotation = Quaternion.identity,
                solvePosition = true,
                solveRotation = false,
                maxIterations = config.fixedMaxIterations,
                positionTolerance = config.positionTolerance,
                stepScale = sweepPoint.stepScale
            };
        }

        private static float ComputeJointRotationDeviationDegrees(ChainState initialState, ChainState finalState)
        {
            if (initialState == null || finalState == null || initialState.joints == null || finalState.joints == null)
            {
                return 0f;
            }

            int jointCount = Mathf.Min(initialState.joints.Length, finalState.joints.Length);
            if (jointCount == 0)
            {
                return 0f;
            }

            float totalDeviation = 0f;
            for (int i = 0; i < jointCount; i++)
            {
                totalDeviation += Quaternion.Angle(initialState.joints[i].localRotation, finalState.joints[i].localRotation);
            }

            return totalDeviation / jointCount;
        }

        private static List<IKBenchmarkStepPoint> BuildStepPoints(IKBenchmarkReport report)
        {
            Dictionary<string, StepAccumulator> accumulators = new Dictionary<string, StepAccumulator>();
            Dictionary<string, IKBenchmarkStepPoint> metadata = new Dictionary<string, IKBenchmarkStepPoint>();

            for (int resultIndex = 0; resultIndex < report.sampleResults.Count; resultIndex++)
            {
                IKBenchmarkSampleResult result = report.sampleResults[resultIndex];
                string key =
                    result.categoryName + "|" +
                    result.seriesName + "|" +
                    result.stepScale.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

                if (!accumulators.TryGetValue(key, out StepAccumulator accumulator))
                {
                    accumulator = new StepAccumulator();
                    metadata[key] = new IKBenchmarkStepPoint
                    {
                        categoryName = result.categoryName,
                        reachableCategory = result.reachableCategory,
                        seriesName = result.seriesName,
                        methodFamily = result.methodFamily,
                        tuningName = result.tuningName,
                        tuningValue = result.tuningValue,
                        stepScale = result.stepScale
                    };
                }

                accumulator.sampleCount++;
                accumulator.totalRuntime += result.elapsedMilliseconds;
                accumulator.totalFinalError += result.finalPositionError;
                accumulator.totalJointRotationDeviation += result.jointRotationDeviationDegrees;

                if (result.converged)
                {
                    accumulator.successCount++;
                    accumulator.successIterations += result.iterations;
                }

                accumulators[key] = accumulator;
            }

            List<IKBenchmarkStepPoint> stepPoints = new List<IKBenchmarkStepPoint>(accumulators.Count);

            foreach (KeyValuePair<string, StepAccumulator> pair in accumulators)
            {
                IKBenchmarkStepPoint point = metadata[pair.Key];
                StepAccumulator accumulator = pair.Value;

                point.sampleCount = accumulator.sampleCount;
                point.successCount = accumulator.successCount;
                point.successRate = accumulator.sampleCount > 0
                    ? (float)accumulator.successCount / accumulator.sampleCount
                    : 0f;
                point.averageConvergedIterations = accumulator.successCount > 0
                    ? accumulator.successIterations / accumulator.successCount
                    : float.NaN;
                point.averageRuntimeAllSamples = accumulator.sampleCount > 0
                    ? accumulator.totalRuntime / accumulator.sampleCount
                    : 0f;
                point.averageFinalError = accumulator.sampleCount > 0
                    ? accumulator.totalFinalError / accumulator.sampleCount
                    : 0f;
                point.averageJointRotationDeviationDegrees = accumulator.sampleCount > 0
                    ? accumulator.totalJointRotationDeviation / accumulator.sampleCount
                    : 0f;

                stepPoints.Add(point);
            }

            return stepPoints;
        }

        private static List<IKBenchmarkScorePoint> BuildScorePoints(IKBenchmarkReport report)
        {
            Dictionary<string, ScorePair> pairs = new Dictionary<string, ScorePair>();

            for (int i = 0; i < report.stepPoints.Count; i++)
            {
                IKBenchmarkStepPoint point = report.stepPoints[i];
                string key =
                    point.seriesName + "|" +
                    point.stepScale.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

                pairs.TryGetValue(key, out ScorePair pair);
                pair.seriesName = point.seriesName;
                pair.methodFamily = point.methodFamily;
                pair.tuningName = point.tuningName;
                pair.tuningValue = point.tuningValue;
                pair.stepScale = point.stepScale;

                if (point.reachableCategory)
                {
                    pair.reachablePoint = point;
                }
                else
                {
                    pair.unreachablePoint = point;
                }

                pairs[key] = pair;
            }

            MetricRangeTracker reachableSuccessRate = new MetricRangeTracker();
            MetricRangeTracker reachableConvergedIterations = new MetricRangeTracker();
            MetricRangeTracker reachableRuntime = new MetricRangeTracker();
            MetricRangeTracker reachableFinalError = new MetricRangeTracker();
            MetricRangeTracker reachableRotationDeviation = new MetricRangeTracker();
            MetricRangeTracker unreachableRuntime = new MetricRangeTracker();
            MetricRangeTracker unreachableFinalError = new MetricRangeTracker();
            MetricRangeTracker unreachableRotationDeviation = new MetricRangeTracker();

            List<ScoreRawMetrics> rawMetrics = new List<ScoreRawMetrics>(pairs.Count);

            foreach (KeyValuePair<string, ScorePair> entry in pairs)
            {
                ScorePair pair = entry.Value;
                if (pair.reachablePoint == null || pair.unreachablePoint == null)
                {
                    continue;
                }

                ScoreRawMetrics metrics = new ScoreRawMetrics
                {
                    seriesName = pair.seriesName,
                    methodFamily = pair.methodFamily,
                    tuningName = pair.tuningName,
                    tuningValue = pair.tuningValue,
                    stepScale = pair.stepScale,
                    reachableSuccessRate = pair.reachablePoint.successRate,
                    reachableAverageConvergedIterations = float.IsNaN(pair.reachablePoint.averageConvergedIterations)
                        ? report.config.fixedMaxIterations
                        : pair.reachablePoint.averageConvergedIterations,
                    reachableAverageRuntimeAllSamples = pair.reachablePoint.averageRuntimeAllSamples,
                    reachableAverageFinalError = pair.reachablePoint.averageFinalError,
                    reachableAverageJointRotationDeviationDegrees = pair.reachablePoint.averageJointRotationDeviationDegrees,
                    unreachableAverageRuntimeAllSamples = pair.unreachablePoint.averageRuntimeAllSamples,
                    unreachableAverageFinalError = pair.unreachablePoint.averageFinalError,
                    unreachableAverageJointRotationDeviationDegrees = pair.unreachablePoint.averageJointRotationDeviationDegrees
                };

                rawMetrics.Add(metrics);
                reachableSuccessRate.Include(metrics.reachableSuccessRate);
                reachableConvergedIterations.Include(metrics.reachableAverageConvergedIterations);
                reachableRuntime.Include(metrics.reachableAverageRuntimeAllSamples);
                reachableFinalError.Include(metrics.reachableAverageFinalError);
                reachableRotationDeviation.Include(metrics.reachableAverageJointRotationDeviationDegrees);
                unreachableRuntime.Include(metrics.unreachableAverageRuntimeAllSamples);
                unreachableFinalError.Include(metrics.unreachableAverageFinalError);
                unreachableRotationDeviation.Include(metrics.unreachableAverageJointRotationDeviationDegrees);
            }

            List<IKBenchmarkScorePoint> scorePoints = new List<IKBenchmarkScorePoint>(rawMetrics.Count);

            for (int i = 0; i < rawMetrics.Count; i++)
            {
                ScoreRawMetrics metrics = rawMetrics[i];

                float compositeScore = 100f * (
                    0.20f * NormalizeHigh(metrics.reachableSuccessRate, reachableSuccessRate) +
                    0.10f * NormalizeLow(metrics.reachableAverageConvergedIterations, reachableConvergedIterations) +
                    0.10f * NormalizeLow(metrics.reachableAverageRuntimeAllSamples, reachableRuntime) +
                    0.10f * NormalizeLow(metrics.reachableAverageFinalError, reachableFinalError) +
                    0.20f * NormalizeLow(metrics.reachableAverageJointRotationDeviationDegrees, reachableRotationDeviation) +
                    0.05f * NormalizeLow(metrics.unreachableAverageRuntimeAllSamples, unreachableRuntime) +
                    0.15f * NormalizeLow(metrics.unreachableAverageFinalError, unreachableFinalError) +
                    0.10f * NormalizeLow(metrics.unreachableAverageJointRotationDeviationDegrees, unreachableRotationDeviation));

                scorePoints.Add(new IKBenchmarkScorePoint
                {
                    seriesName = metrics.seriesName,
                    methodFamily = metrics.methodFamily,
                    tuningName = metrics.tuningName,
                    tuningValue = metrics.tuningValue,
                    stepScale = metrics.stepScale,
                    compositeScore = compositeScore,
                    reachableSuccessRate = metrics.reachableSuccessRate,
                    reachableAverageConvergedIterations = metrics.reachableAverageConvergedIterations,
                    reachableAverageRuntimeAllSamples = metrics.reachableAverageRuntimeAllSamples,
                    reachableAverageFinalError = metrics.reachableAverageFinalError,
                    reachableAverageJointRotationDeviationDegrees = metrics.reachableAverageJointRotationDeviationDegrees,
                    unreachableAverageRuntimeAllSamples = metrics.unreachableAverageRuntimeAllSamples,
                    unreachableAverageFinalError = metrics.unreachableAverageFinalError,
                    unreachableAverageJointRotationDeviationDegrees = metrics.unreachableAverageJointRotationDeviationDegrees
                });
            }

            return scorePoints;
        }

        private static List<IKBenchmarkBestScorePoint> BuildBestScorePoints(IReadOnlyList<IKBenchmarkScorePoint> scorePoints)
        {
            Dictionary<string, IKBenchmarkBestScorePoint> bestBySeries = new Dictionary<string, IKBenchmarkBestScorePoint>();

            for (int i = 0; i < scorePoints.Count; i++)
            {
                IKBenchmarkScorePoint point = scorePoints[i];
                if (!bestBySeries.TryGetValue(point.seriesName, out IKBenchmarkBestScorePoint bestPoint) ||
                    point.compositeScore > bestPoint.compositeScore)
                {
                    bestBySeries[point.seriesName] = new IKBenchmarkBestScorePoint
                    {
                        seriesName = point.seriesName,
                        methodFamily = point.methodFamily,
                        tuningName = point.tuningName,
                        tuningValue = point.tuningValue,
                        bestStepScale = point.stepScale,
                        compositeScore = point.compositeScore
                    };
                }
            }

            return new List<IKBenchmarkBestScorePoint>(bestBySeries.Values);
        }

        private static float NormalizeHigh(float value, MetricRangeTracker range)
        {
            float span = range.max - range.min;
            if (span < 1e-6f)
            {
                return 1f;
            }

            float denominator = span;
            return Mathf.Clamp01((value - range.min) / denominator);
        }

        private static float NormalizeLow(float value, MetricRangeTracker range)
        {
            float span = range.max - range.min;
            if (span < 1e-6f)
            {
                return 1f;
            }

            float denominator = span;
            return Mathf.Clamp01((range.max - value) / denominator);
        }

        private static void SortStepPoints(List<IKBenchmarkStepPoint> stepPoints)
        {
            stepPoints.Sort((a, b) =>
            {
                int categoryCompare = string.CompareOrdinal(a.categoryName, b.categoryName);
                if (categoryCompare != 0)
                {
                    return categoryCompare;
                }

                int stepCompare = a.stepScale.CompareTo(b.stepScale);
                if (stepCompare != 0)
                {
                    return stepCompare;
                }

                return string.CompareOrdinal(a.seriesName, b.seriesName);
            });
        }

        private static void SortScorePoints(List<IKBenchmarkScorePoint> scorePoints)
        {
            scorePoints.Sort((a, b) =>
            {
                int stepCompare = a.stepScale.CompareTo(b.stepScale);
                if (stepCompare != 0)
                {
                    return stepCompare;
                }

                return string.CompareOrdinal(a.seriesName, b.seriesName);
            });
        }

        private static void SortBestScorePoints(List<IKBenchmarkBestScorePoint> bestScorePoints)
        {
            bestScorePoints.Sort((a, b) => b.compositeScore.CompareTo(a.compositeScore));
        }

        private static Vector3 SampleDirection(System.Random random)
        {
            float z = Mathf.Lerp(-1f, 1f, NextFloat(random));
            float angle = Mathf.Lerp(0f, Mathf.PI * 2f, NextFloat(random));
            float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));

            return new Vector3(
                radial * Mathf.Cos(angle),
                radial * Mathf.Sin(angle),
                z);
        }

        private static float NextFloat(System.Random random)
        {
            return (float)random.NextDouble();
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = (hash * 31) + value[i];
                }

                return hash;
            }
        }

        private static ChainState CloneState(ChainState source)
        {
            ChainState clone = new ChainState
            {
                rootWorldPosition = source.rootWorldPosition,
                rootWorldRotation = source.rootWorldRotation,
                endEffectorPosition = source.endEffectorPosition,
                endEffectorRotation = source.endEffectorRotation
            };

            if (source.joints == null)
            {
                return clone;
            }

            clone.joints = new JointState[source.joints.Length];
            Array.Copy(source.joints, clone.joints, source.joints.Length);
            return clone;
        }

        private struct StepAccumulator
        {
            public int sampleCount;
            public int successCount;
            public float successIterations;
            public float totalRuntime;
            public float totalFinalError;
            public float totalJointRotationDeviation;
        }

        private struct MetricRangeTracker
        {
            public float min;
            public float max;
            public bool initialized;

            public void Include(float value)
            {
                if (!initialized)
                {
                    min = value;
                    max = value;
                    initialized = true;
                    return;
                }

                if (value < min)
                {
                    min = value;
                }

                if (value > max)
                {
                    max = value;
                }
            }
        }

        private struct ScorePair
        {
            public string seriesName;
            public string methodFamily;
            public string tuningName;
            public float tuningValue;
            public float stepScale;
            public IKBenchmarkStepPoint reachablePoint;
            public IKBenchmarkStepPoint unreachablePoint;
        }

        private struct ScoreRawMetrics
        {
            public string seriesName;
            public string methodFamily;
            public string tuningName;
            public float tuningValue;
            public float stepScale;
            public float reachableSuccessRate;
            public float reachableAverageConvergedIterations;
            public float reachableAverageRuntimeAllSamples;
            public float reachableAverageFinalError;
            public float reachableAverageJointRotationDeviationDegrees;
            public float unreachableAverageRuntimeAllSamples;
            public float unreachableAverageFinalError;
            public float unreachableAverageJointRotationDeviationDegrees;
        }

        private sealed class BenchmarkChainFixture : IDisposable
        {
            private readonly GameObject rootObject;

            public ChainDefinition Definition { get; private set; }
            public ChainState InitialState { get; private set; }
            public float TotalReach { get; private set; }

            private BenchmarkChainFixture(GameObject rootObject)
            {
                this.rootObject = rootObject;
            }

            public static BenchmarkChainFixture Create(IReadOnlyList<float> boneLengths)
            {
                GameObject rootAnchor = new GameObject("__IKBenchmarkRoot");
                Transform rootParent = rootAnchor.transform;
                rootParent.position = Vector3.zero;
                rootParent.rotation = Quaternion.identity;

                int jointCount = boneLengths.Count;
                Transform[] joints = new Transform[jointCount];

                for (int i = 0; i < jointCount; i++)
                {
                    GameObject jointObject = new GameObject("BenchmarkJoint_" + i);
                    Transform jointTransform = jointObject.transform;
                    jointTransform.SetParent(i == 0 ? rootParent : joints[i - 1], false);
                    jointTransform.localPosition = i == 0 ? Vector3.zero : Vector3.forward * boneLengths[i - 1];
                    jointTransform.localRotation = Quaternion.identity;
                    joints[i] = jointTransform;
                }

                JointDefinition[] definitions = new JointDefinition[jointCount];
                for (int i = 0; i < jointCount; i++)
                {
                    definitions[i] = new JointDefinition
                    {
                        index = i,
                        parentIndex = i - 1,
                        name = joints[i].name,
                        transform = joints[i],
                        localBindOffset = i == 0 ? Vector3.zero : Vector3.forward * boneLengths[i - 1],
                        isTerminal = i == jointCount - 1,
                        terminalBoneLength = i == jointCount - 1 ? boneLengths[i] : 0f,
                        restLocalRotation = Quaternion.identity,
                        axes = new[]
                        {
                            new JointAxis("X", Vector3.right, true),
                            new JointAxis("Y", Vector3.up, true),
                            new JointAxis("Z", Vector3.forward, true)
                        },
                        limit = JointLimit.Default,
                        weight = 1f,
                        locked = false
                    };
                }

                ChainDefinition definition = new ChainDefinition
                {
                    name = "BenchmarkChain",
                    root = joints[0],
                    endEffector = joints[jointCount - 1],
                    joints = definitions
                };

                ChainState initialState = new ChainState
                {
                    rootWorldPosition = joints[0].position,
                    rootWorldRotation = joints[0].parent != null ? joints[0].parent.rotation : Quaternion.identity,
                    joints = new JointState[jointCount]
                };

                for (int i = 0; i < jointCount; i++)
                {
                    initialState.joints[i] = new JointState(
                        joints[i].localRotation,
                        joints[i].position,
                        joints[i].rotation);
                }

                ForwardKinematics.Evaluate(definition, initialState);

                float totalReach = 0f;
                for (int i = 0; i < boneLengths.Count; i++)
                {
                    totalReach += boneLengths[i];
                }

                return new BenchmarkChainFixture(rootAnchor)
                {
                    Definition = definition,
                    InitialState = initialState,
                    TotalReach = totalReach
                };
            }

            public void Dispose()
            {
                if (rootObject != null)
                {
                    Object.DestroyImmediate(rootObject);
                }
            }
        }
    }
}
