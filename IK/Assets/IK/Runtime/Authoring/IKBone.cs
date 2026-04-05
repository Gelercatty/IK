using GelerIK.Runtime.Model;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GelerIK.Runtime.Authoring
{
    [ExecuteAlways]
    public class IKBone : MonoBehaviour
    {
        private const string MeshPreviewObjectName = "__IKBoneMeshPreview";
        private bool meshPreviewSyncPending;

        [Header("Hierarchy")]
        [SerializeField] private IKBone parentBone;
        [SerializeField] private float boneLength = 1f;
        [SerializeField] private bool alignToParentBoneEnd;

        [Header("Degrees Of Freedom")]
        [SerializeField] private bool useAxisX = true;
        [SerializeField] private bool useAxisY = true;
        [SerializeField] private bool useAxisZ = true;

        [Header("Local Axes")]
        [SerializeField] private Vector3 localAxisX = Vector3.right;
        [SerializeField] private Vector3 localAxisY = Vector3.up;
        [SerializeField] private Vector3 localAxisZ = Vector3.forward;

        [Header("Limits (Degrees)")]
        [SerializeField] private Vector2 limitX = new(-180f, 180f);
        [SerializeField] private Vector2 limitY = new(-180f, 180f);
        [SerializeField] private Vector2 limitZ = new(-180f, 180f);

        [Header("Options")]
        [SerializeField] private float weight = 1f;
        [SerializeField] private bool locked;
        [SerializeField] private Quaternion restLocalRotation = Quaternion.identity;

        [Header("Visualization")]
        [SerializeField] private bool showBone = true;
        [SerializeField] private Color boneColor = new(0.85f, 0.8f, 0.35f, 1f);
        [SerializeField] private float boneRadius = 0.04f;
        [SerializeField] private bool showAxes = true;
        [SerializeField] private float axisLength = 0.3f;
        [SerializeField] private bool showMeshPreview;

        public IKBone ParentBone => parentBone;
        public Quaternion RestLocalRotation => restLocalRotation;
        public float BoneLength => boneLength;

        private void Reset()
        {
            restLocalRotation = transform.localRotation;
            parentBone = transform.parent == null ? null : transform.parent.GetComponent<IKBone>();
            boneLength = GetDefaultBoneLength();
        }

        private void OnValidate()
        {
            localAxisX = NormalizeOrDefault(localAxisX, Vector3.right);
            localAxisY = NormalizeOrDefault(localAxisY, Vector3.up);
            localAxisZ = NormalizeOrDefault(localAxisZ, Vector3.forward);
            weight = Mathf.Max(0f, weight);
            boneLength = Mathf.Max(0f, boneLength);
            boneRadius = Mathf.Max(0.001f, boneRadius);
            axisLength = Mathf.Max(0.01f, axisLength);

            TryAlignToParentBoneEnd();
            RequestMeshPreviewSync();
        }

        [ContextMenu("Capture Rest Local Rotation")]
        public void CaptureRestLocalRotation()
        {
            restLocalRotation = transform.localRotation;
        }

        [ContextMenu("Guess Bone Length From Child")]
        public void GuessBoneLengthFromChild()
        {
            boneLength = GetDefaultBoneLength();
        }

        public JointDefinition CreateDefinition(int index, int parentIndex)
        {
            return new JointDefinition
            {
                index = index,
                parentIndex = parentIndex,
                name = gameObject.name,
                transform = transform,
                localBindOffset = transform.localPosition,
                boneLength = boneLength,
                restLocalRotation = restLocalRotation,
                axes = CreateAxes(),
                limit = new JointLimit(limitX, limitY, limitZ),
                weight = weight,
                locked = locked
            };
        }

        public bool ApplyJointDefinition(JointDefinition definition, bool applyTransformValues = false)
        {
            if (definition == null)
            {
                return false;
            }

            boneLength = definition.boneLength;
            restLocalRotation = definition.restLocalRotation;
            weight = definition.weight;
            locked = definition.locked;

            limitX = definition.limit.xDegrees;
            limitY = definition.limit.yDegrees;
            limitZ = definition.limit.zDegrees;

            ApplyAxes(definition.axes);

            if (applyTransformValues)
            {
                transform.localPosition = definition.localBindOffset;
                transform.localRotation = definition.restLocalRotation;
            }

            return true;
        }

        public void SetMeshPreviewEnabled(bool enabled)
        {
            showMeshPreview = enabled;
            RequestMeshPreviewSync();
        }

        private void Update()
        {
            TryAlignToParentBoneEnd();
            if (meshPreviewSyncPending)
            {
                meshPreviewSyncPending = false;
                SyncMeshPreviewObject();
            }
        }

        private JointAxis[] CreateAxes()
        {
            return new[]
            {
                new JointAxis("X", localAxisX, useAxisX),
                new JointAxis("Y", localAxisY, useAxisY),
                new JointAxis("Z", localAxisZ, useAxisZ)
            };
        }

        private void ApplyAxes(JointAxis[] axes)
        {
            localAxisX = Vector3.right;
            localAxisY = Vector3.up;
            localAxisZ = Vector3.forward;
            useAxisX = false;
            useAxisY = false;
            useAxisZ = false;

            if (axes == null)
            {
                return;
            }

            foreach (JointAxis axis in axes)
            {
                switch (axis.name)
                {
                    case "X":
                        localAxisX = NormalizeOrDefault(axis.localAxis, Vector3.right);
                        useAxisX = axis.enabled;
                        break;
                    case "Y":
                        localAxisY = NormalizeOrDefault(axis.localAxis, Vector3.up);
                        useAxisY = axis.enabled;
                        break;
                    case "Z":
                        localAxisZ = NormalizeOrDefault(axis.localAxis, Vector3.forward);
                        useAxisZ = axis.enabled;
                        break;
                }
            }
        }

        private float GetDefaultBoneLength()
        {
            if (transform.childCount > 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child.name == MeshPreviewObjectName)
                    {
                        continue;
                    }

                    return child.localPosition.magnitude;
                }
            }

            float currentMagnitude = transform.localPosition.magnitude;
            return currentMagnitude > 1e-6f ? currentMagnitude : 1f;
        }

        private static Vector3 NormalizeOrDefault(Vector3 axis, Vector3 fallback)
        {
            return axis.sqrMagnitude < 1e-6f ? fallback : axis.normalized;
        }

        private void AlignToParentBoneEnd()
        {
            Transform parentTransform = parentBone.transform;
            Vector3 parentBoneEndWorld =
                parentTransform.position + parentTransform.rotation * (Vector3.forward * parentBone.boneLength);

            if (transform.parent == parentTransform)
            {
                transform.localPosition = Vector3.forward * parentBone.boneLength;
                return;
            }

            if (transform.parent != null)
            {
                transform.localPosition = transform.parent.InverseTransformPoint(parentBoneEndWorld);
                return;
            }

            transform.position = parentBoneEndWorld;
        }

        private void TryAlignToParentBoneEnd()
        {
            if (!alignToParentBoneEnd || parentBone == null)
            {
                return;
            }

            AlignToParentBoneEnd();
        }

        private void RequestMeshPreviewSync()
        {
            meshPreviewSyncPending = true;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.delayCall -= ExecutePendingMeshPreviewSync;
                EditorApplication.delayCall += ExecutePendingMeshPreviewSync;
            }
#endif
        }

#if UNITY_EDITOR
        private void ExecutePendingMeshPreviewSync()
        {
            if (this == null)
            {
                return;
            }

            if (!meshPreviewSyncPending)
            {
                return;
            }

            meshPreviewSyncPending = false;
            SyncMeshPreviewObject();
        }
#endif

        private void SyncMeshPreviewObject()
        {
            Transform previewTransform = transform.Find(MeshPreviewObjectName);

            if (!showMeshPreview)
            {
                if (previewTransform != null)
                {
                    DestroyPreviewObject(previewTransform.gameObject);
                }

                return;
            }

            GameObject previewObject = previewTransform != null
                ? previewTransform.gameObject
                : CreatePreviewObject();

            previewObject.name = MeshPreviewObjectName;
            previewObject.transform.SetParent(transform, false);
            previewObject.transform.localPosition = new Vector3(0f, 0f, 0.1f);
            previewObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            previewObject.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);

            MeshFilter meshFilter = previewObject.GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = previewObject.AddComponent<MeshFilter>();
            }

            MeshRenderer meshRenderer = previewObject.GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = previewObject.AddComponent<MeshRenderer>();
            }

            EnsureCapsulePresentation(previewObject, meshFilter, meshRenderer);
        }

        private GameObject CreatePreviewObject()
        {
            GameObject previewObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            previewObject.name = MeshPreviewObjectName;
            previewObject.transform.SetParent(transform, false);

            Collider collider = previewObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            return previewObject;
        }

        private void EnsureCapsulePresentation(GameObject previewObject, MeshFilter meshFilter, MeshRenderer meshRenderer)
        {
            Collider collider = previewObject.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            if (meshFilter.sharedMesh == null)
            {
                MeshFilter existingMeshFilter = previewObject.GetComponent<MeshFilter>();
                if (existingMeshFilter != null)
                {
                    meshFilter.sharedMesh = existingMeshFilter.sharedMesh;
                }
            }

            if (meshRenderer.sharedMaterial == null)
            {
                MeshRenderer existingMeshRenderer = previewObject.GetComponent<MeshRenderer>();
                if (existingMeshRenderer != null)
                {
                    meshRenderer.sharedMaterial = existingMeshRenderer.sharedMaterial;
                }
            }
        }

        private void DestroyPreviewObject(Object targetObject)
        {
            if (targetObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(targetObject);
            }
            else
            {
#if UNITY_EDITOR
                EditorApplication.delayCall += () =>
                {
                    if (targetObject != null)
                    {
                        DestroyImmediate(targetObject);
                    }
                };
#else
                Destroy(targetObject);
#endif
            }
        }
        
        
        
        // visualize
        private void OnDrawGizmos()
        {
            if (showBone)
            {
                DrawBoneGizmo();
            }

            if (showAxes)
            {
                DrawAxisGizmos();
            }
        }

        private void DrawBoneGizmo()
        {
            Vector3 start = transform.position;
            Vector3 end = start + transform.rotation * (Vector3.forward * boneLength);
            Gizmos.color = boneColor;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(start, boneRadius);

            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(end, transform.rotation, Vector3.one);
            float width = boneRadius * 0.8f;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, width, width * 1.5f));
            Gizmos.matrix = previousMatrix;
        }

        private void DrawAxisGizmos()
        {
            Vector3 origin = transform.position;

            if (useAxisX)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, origin + transform.rotation * (localAxisX * axisLength));
            }

            if (useAxisY)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(origin, origin + transform.rotation * (localAxisY * axisLength));
            }

            if (useAxisZ)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(origin, origin + transform.rotation * (localAxisZ * axisLength));
            }
        }
    }
}
