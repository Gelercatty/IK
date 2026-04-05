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
            IList<IKSolverRegistration> solverRegistrations,
            IList<IKBenchmarkScenario> scenarios)
        {
            IKBenchmarkReport report = new IKBenchmarkReport
            {
                config = config
            };

            using (BenchmarkChainFixture fixture = BenchmarkChainFixture.Create(config.boneLengths))
            {
                for (int scenarioIndex = 0; scenarioIndex < scenarios.Count; scenarioIndex++)
                {
                    IKBenchmarkScenario scenario = scenarios[scenarioIndex];
                    Vector3[] targets = BuildTargets(config, fixture.TotalReach, scenario, scenarioIndex);

                    for (int solverIndex = 0; solverIndex < solverRegistrations.Count; solverIndex++)
                    {
                        IKSolverRegistration solverRegistration = solverRegistrations[solverIndex];
                        IIKSolver solver = solverRegistration.createSolver();

                        WarmupSolver(config, fixture, solverRegistration, solver, targets);
                        List<IKBenchmarkSampleResult> sampleResults =
                            RunScenario(config, fixture, solverRegistration, scenario, solver, targets);

                        report.summaries.Add(BuildSummary(solverRegistration.name, scenario.name, sampleResults));
                    }
                }
            }

            return report;
        }

        private static void WarmupSolver(
            IKBenchmarkConfig config,
            BenchmarkChainFixture fixture,
            IKSolverRegistration solverRegistration,
            IIKSolver solver,
            IReadOnlyList<Vector3> targets)
        {
            int warmupCount = Mathf.Min(config.warmupSamples, targets.Count);

            for (int i = 0; i < warmupCount; i++)
            {
                IKSolveRequest request = CreateRequest(config, fixture, solverRegistration, targets[i]);
                solver.Solve(request);
            }
        }

        private static List<IKBenchmarkSampleResult> RunScenario(
            IKBenchmarkConfig config,
            BenchmarkChainFixture fixture,
            IKSolverRegistration solverRegistration,
            IKBenchmarkScenario scenario,
            IIKSolver solver,
            IReadOnlyList<Vector3> targets)
        {
            List<IKBenchmarkSampleResult> sampleResults = new(config.samplesPerScenario);

            for (int i = config.warmupSamples; i < targets.Count; i++)
            {
                Vector3 target = targets[i];
                IKSolveRequest request = CreateRequest(config, fixture, solverRegistration, target);

                Stopwatch stopwatch = Stopwatch.StartNew();
                IKSolveResult solveResult = solver.Solve(request);
                stopwatch.Stop();

                sampleResults.Add(new IKBenchmarkSampleResult
                {
                    solverName = solverRegistration.name,
                    scenarioName = scenario.name,
                    sampleIndex = i - config.warmupSamples,
                    converged = solveResult != null && solveResult.converged,
                    iterations = solveResult != null ? solveResult.iterations : 0,
                    elapsedMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds,
                    finalPositionError = solveResult != null ? solveResult.finalPositionError : float.PositiveInfinity,
                    targetDistance = target.magnitude
                });
            }

            return sampleResults;
        }

        private static IKSolveRequest CreateRequest(
            IKBenchmarkConfig config,
            BenchmarkChainFixture fixture,
            IKSolverRegistration solverRegistration,
            Vector3 target)
        {
            return new IKSolveRequest
            {
                definition = fixture.Definition,
                state = CloneState(fixture.InitialState),
                targetPosition = target,
                targetRotation = Quaternion.identity,
                solvePosition = true,
                solveRotation = false,
                maxIterations = config.maxIterations,
                positionTolerance = config.positionTolerance,
                stepScale = solverRegistration.stepScale > 0f ? solverRegistration.stepScale : config.stepScale
            };
        }

        private static IKBenchmarkSummary BuildSummary(
            string solverName,
            string scenarioName,
            List<IKBenchmarkSampleResult> sampleResults)
        {
            IKBenchmarkSummary summary = new IKBenchmarkSummary
            {
                solverName = solverName,
                scenarioName = scenarioName,
                totalSamples = sampleResults.Count
            };

            float totalElapsed = 0f;
            float totalIterations = 0f;
            float totalFinalError = 0f;
            float successElapsed = 0f;
            float successIterations = 0f;
            float failureFinalError = 0f;

            for (int i = 0; i < sampleResults.Count; i++)
            {
                IKBenchmarkSampleResult sample = sampleResults[i];
                totalElapsed += sample.elapsedMilliseconds;
                totalIterations += sample.iterations;
                totalFinalError += sample.finalPositionError;

                if (sample.converged)
                {
                    summary.successCount++;
                    successElapsed += sample.elapsedMilliseconds;
                    successIterations += sample.iterations;
                }
                else
                {
                    failureFinalError += sample.finalPositionError;
                }
            }

            summary.successRate = summary.totalSamples > 0 ? (float)summary.successCount / summary.totalSamples : 0f;
            summary.averageElapsedMillisecondsAllSamples =
                summary.totalSamples > 0 ? totalElapsed / summary.totalSamples : 0f;
            summary.averageIterationsAllSamples =
                summary.totalSamples > 0 ? totalIterations / summary.totalSamples : 0f;
            summary.averageFinalPositionError =
                summary.totalSamples > 0 ? totalFinalError / summary.totalSamples : 0f;
            summary.averageElapsedMillisecondsOnSuccess =
                summary.successCount > 0 ? successElapsed / summary.successCount : 0f;
            summary.averageIterationsOnSuccess =
                summary.successCount > 0 ? successIterations / summary.successCount : 0f;

            int failureCount = summary.totalSamples - summary.successCount;
            summary.averageFailureFinalPositionError = failureCount > 0 ? failureFinalError / failureCount : 0f;

            return summary;
        }

        private static Vector3[] BuildTargets(
            IKBenchmarkConfig config,
            float totalReach,
            IKBenchmarkScenario scenario,
            int scenarioIndex)
        {
            int sampleCount = config.samplesPerScenario + config.warmupSamples;
            Vector3[] targets = new Vector3[sampleCount];
            System.Random random = new System.Random(config.randomSeed + scenarioIndex * 9973);

            for (int i = 0; i < sampleCount; i++)
            {
                float radius = totalReach * Mathf.Lerp(
                    scenario.minRadiusScale,
                    scenario.maxRadiusScale,
                    NextFloat(random));

                targets[i] = SampleDirection(random) * radius;
            }

            return targets;
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
