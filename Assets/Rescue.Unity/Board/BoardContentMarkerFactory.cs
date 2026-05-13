using Rescue.Core.State;
using UnityEngine;

namespace Rescue.Unity.BoardPresentation
{
    internal static class BoardContentMarkerFactory
    {
        private const string RescuePathWashName = "RescuePathWash";
        private const string RescuePathPawName = "RescuePathPaw";
        private const string RescuePathPawModelResourcePath = "Meshy_AI_Teal_Paw_0513020745_texture";
        private const string RescuePathPawTextureResourcePath = "Textures/Meshy_AI_Teal_Paw_0513020745_texture";
        private const string TargetReadabilityMarkerName = "TargetReadabilityMarker";
        private const int TargetMarkerCircleSegments = 32;

        private static readonly Vector3 TargetReadabilityMarkerLocalPosition = new Vector3(0f, 0.025f, 0f);
        private static readonly Vector3 TargetReadabilityMarkerLocalScale = new Vector3(0.7f, 1f, 0.7f);
        private static readonly Vector3 TargetLastObstacleMarkerLocalPosition = new Vector3(0f, 0.035f, 0f);
        private static readonly Vector3 TargetLastObstacleMarkerLocalScale = new Vector3(0.6f, 1f, 0.6f);
        private static readonly Color RescuePathWashColor = new Color(0.118f, 0.522f, 0.467f, 0.24f);
        private static readonly Color RescuePathPawTintColor = new Color(0.55f, 0.93f, 0.84f, 0.9f);
        private static readonly Color TargetMarkerNeutralColor = new Color(0.9f, 0.9f, 0.84f, 0.5f);

        private static Material? rescuePathWashMaterial;
        private static Material? rescuePathPawMaterial;
        private static Material? targetMarkerMaterial;

        public static GameObject CreateRescuePathMarker(
            TileCoord coord,
            string contentLabel,
            Transform parent,
            Transform anchor,
            Vector3 worldPosition,
            float yOffset,
            Vector3 outwardDirection)
        {
            GameObject markerObject = new GameObject($"Content_{coord.Row:00}_{coord.Col:00}_{contentLabel}");
            AttachOrUpdateBoardCellView(markerObject, coord);
            Transform markerTransform = markerObject.transform;
            markerTransform.SetParent(parent, worldPositionStays: false);

            if (parent == anchor)
            {
                markerTransform.localPosition = new Vector3(0f, yOffset, 0f);
                markerTransform.localRotation = Quaternion.identity;
            }
            else
            {
                markerTransform.SetPositionAndRotation(worldPosition, anchor.rotation);
            }

            markerTransform.localScale = Vector3.one;
            CreateRescuePathWash(markerTransform);
            CreateRescuePathPaw(markerTransform);
            ConfigureRescuePathMarker(markerObject, outwardDirection);
            return markerObject;
        }

        public static void ConfigureRescuePathMarker(GameObject markerObject, Vector3 outwardDirection)
        {
            Vector3 flattened = new Vector3(outwardDirection.x, 0f, outwardDirection.z);
            if (flattened.sqrMagnitude <= 0.0001f)
            {
                flattened = Vector3.forward;
            }

            markerObject.transform.localRotation = Quaternion.LookRotation(flattened.normalized, Vector3.up);
        }

        public static void SyncTargetReadabilityMarker(GameObject targetObject, TargetReadiness readiness)
        {
            Transform? marker = FindChildByNamePrefix(targetObject.transform, TargetReadabilityMarkerName);
            bool needsMarker = readiness != TargetReadiness.Trapped;
            if (!needsMarker)
            {
                if (marker is not null)
                {
                    DestroyMarker(marker.gameObject);
                }

                return;
            }

            if (marker is null)
            {
                GameObject markerObject = CreateTargetMarkerCircle(TargetReadabilityMarkerName);
                markerObject.transform.SetParent(targetObject.transform, false);
                AssignDefaultParticleSystemMaterial(markerObject);
                marker = markerObject.transform;
            }

            marker.gameObject.name = $"{TargetReadabilityMarkerName}_{readiness}";
            marker.localPosition = TargetReadabilityMarkerLocalPosition;
            marker.localScale = TargetReadabilityMarkerLocalScale;
            AssignDefaultParticleSystemMaterial(marker.gameObject);
            ApplyMarkerColor(marker.gameObject);
        }

        public static GameObject CreateLastObstacleMarker(string markerName, Transform parent)
        {
            GameObject marker = CreateTargetMarkerCircle(markerName);
            marker.name = markerName;
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = TargetLastObstacleMarkerLocalPosition;
            marker.transform.localScale = TargetLastObstacleMarkerLocalScale;
            AssignDefaultParticleSystemMaterial(marker);
            ApplyMarkerColor(marker);
            return marker;
        }

        public static void DestroyMarker(GameObject markerObject)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(markerObject);
            }
            else
            {
                Object.DestroyImmediate(markerObject);
            }
        }

        public static void RemoveCollider(GameObject contentObject)
        {
            Collider? collider = contentObject.GetComponent<Collider>();
            if (collider is null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(collider);
            }
            else
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void CreateRescuePathWash(Transform parent)
        {
            GameObject wash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wash.name = RescuePathWashName;
            RemoveCollider(wash);
            wash.transform.SetParent(parent, worldPositionStays: false);
            wash.transform.localPosition = new Vector3(0f, 0.004f, 0f);
            wash.transform.localRotation = Quaternion.identity;
            wash.transform.localScale = new Vector3(0.84f, 0.012f, 0.84f);
            AssignGeneratedRescuePathWashMaterial(wash);
        }

        private static void CreateRescuePathPaw(Transform parent)
        {
            GameObject paw = new GameObject(RescuePathPawName);
            paw.transform.SetParent(parent, worldPositionStays: false);
            paw.transform.localPosition = new Vector3(0f, 0.034f, 0f);
            paw.transform.localRotation = Quaternion.identity;
            paw.transform.localScale = Vector3.one;

            GameObject? pawResource = Resources.Load<GameObject>(RescuePathPawModelResourcePath);
            GameObject pawVisual = pawResource is null
                ? CreateFallbackRescuePathPawVisual()
                : Object.Instantiate(pawResource);

            pawVisual.name = "Visual";
            pawVisual.transform.SetParent(paw.transform, worldPositionStays: false);
            pawVisual.transform.localPosition = Vector3.zero;
            pawVisual.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            pawVisual.transform.localScale = Vector3.one;

            RemoveColliders(pawVisual);
            AssignRescuePathPawMaterial(pawVisual);
            DisableRendererShadows(pawVisual);
            FitVisualToRescuePathTile(pawVisual);
        }

        private static GameObject CreateFallbackRescuePathPawVisual()
        {
            GameObject fallback = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fallback.transform.localScale = new Vector3(0.64f, 0.018f, 0.46f);
            RemoveCollider(fallback);
            return fallback;
        }

        private static void FitVisualToRescuePathTile(GameObject visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 size = bounds.size;
            float footprint = Mathf.Max(size.x, size.z);
            if (footprint > 0.0001f)
            {
                float scale = Mathf.Min(1f, 0.72f / footprint);
                visual.transform.localScale *= scale;
            }

            Vector3 center = bounds.center;
            visual.transform.position -= new Vector3(center.x, bounds.min.y, center.z);
        }

        private static void RemoveColliders(GameObject contentObject)
        {
            Collider[] colliders = contentObject.GetComponentsInChildren<Collider>(includeInactive: true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(colliders[i]);
                }
                else
                {
                    Object.DestroyImmediate(colliders[i]);
                }
            }
        }

        private static GameObject CreateTargetMarkerCircle(string markerName)
        {
            GameObject marker = new GameObject(markerName);
            MeshFilter meshFilter = marker.AddComponent<MeshFilter>();
            MeshRenderer renderer = marker.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = CreateTargetMarkerCircleMesh();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return marker;
        }

        private static Mesh CreateTargetMarkerCircleMesh()
        {
            Vector3[] vertices = new Vector3[TargetMarkerCircleSegments + 2];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[TargetMarkerCircleSegments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uv[0] = new Vector2(0.5f, 0.5f);

            for (int i = 0; i <= TargetMarkerCircleSegments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / TargetMarkerCircleSegments;
                float x = Mathf.Cos(angle) * 0.5f;
                float z = Mathf.Sin(angle) * 0.5f;
                int vertexIndex = i + 1;
                vertices[vertexIndex] = new Vector3(x, 0f, z);
                normals[vertexIndex] = Vector3.up;
                uv[vertexIndex] = new Vector2(x + 0.5f, z + 0.5f);
            }

            for (int i = 0; i < TargetMarkerCircleSegments; i++)
            {
                int triangleIndex = i * 3;
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = i + 1;
                triangles[triangleIndex + 2] = i + 2;
            }

            Mesh mesh = new Mesh
            {
                name = "TargetMarkerCircleMesh",
                vertices = vertices,
                normals = normals,
                uv = uv,
                triangles = triangles,
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AssignDefaultParticleSystemMaterial(GameObject markerObject)
        {
            Renderer? renderer = markerObject.GetComponent<Renderer>();
            Material? markerMaterial = GetDefaultParticleSystemMaterial();
            if (renderer is not null && markerMaterial is not null)
            {
                renderer.sharedMaterial = markerMaterial;
            }
        }

        private static Material? GetDefaultParticleSystemMaterial()
        {
            if (targetMarkerMaterial == null)
            {
                GameObject resolver = new GameObject("DefaultParticleSystemMaterialResolver");
                try
                {
                    ParticleSystem particleSystem = resolver.AddComponent<ParticleSystem>();
                    ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
                    targetMarkerMaterial = renderer.sharedMaterial;
                }
                finally
                {
                    if (Application.isPlaying)
                    {
                        Object.Destroy(resolver);
                    }
                    else
                    {
                        Object.DestroyImmediate(resolver);
                    }
                }

                if (targetMarkerMaterial == null)
                {
                    Shader? shader = Shader.Find("Particles/Standard Unlit");
                    shader ??= Shader.Find("Legacy Shaders/Particles/Alpha Blended");
                    shader ??= Shader.Find("Standard");
                    if (shader is not null)
                    {
                        targetMarkerMaterial = new Material(shader)
                        {
                            name = "Default-ParticleSystem",
                        };
                    }
                }
            }

            return targetMarkerMaterial;
        }

        private static Transform? FindChildByNamePrefix(Transform parent, string namePrefix)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(namePrefix, System.StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static void ApplyMarkerColor(GameObject markerObject)
        {
            Renderer? renderer = markerObject.GetComponent<Renderer>();
            if (renderer is null)
            {
                return;
            }

            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", TargetMarkerNeutralColor);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private static void AssignGeneratedRescuePathWashMaterial(GameObject contentObject)
        {
            Renderer? renderer = contentObject.GetComponent<Renderer>();
            if (renderer is not null)
            {
                renderer.sharedMaterial = GetRescuePathWashMaterial();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static Material GetRescuePathWashMaterial()
        {
            if (rescuePathWashMaterial == null)
            {
                rescuePathWashMaterial = CreateGeneratedTransparentMaterial(RescuePathWashColor);
            }

            return rescuePathWashMaterial;
        }

        private static Material GetRescuePathPawMaterial()
        {
            if (rescuePathPawMaterial == null)
            {
                rescuePathPawMaterial = CreateGeneratedTransparentMaterial(RescuePathPawTintColor);
                Texture2D? texture = Resources.Load<Texture2D>(RescuePathPawTextureResourcePath);
                if (texture is not null)
                {
                    rescuePathPawMaterial.mainTexture = texture;
                }
            }

            return rescuePathPawMaterial;
        }

        private static void AssignRescuePathPawMaterial(GameObject contentObject)
        {
            Renderer[] renderers = contentObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            Material material = GetRescuePathPawMaterial();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = material;
            }
        }

        private static void DisableRendererShadows(GameObject contentObject)
        {
            Renderer[] renderers = contentObject.GetComponentsInChildren<Renderer>(includeInactive: true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
            }
        }

        private static Material CreateGeneratedTransparentMaterial(Color color)
        {
            Shader? shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            shader ??= Shader.Find("Particles/Standard Unlit");
            shader ??= Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            shader ??= Shader.Find("Unlit/Transparent");
            shader ??= Shader.Find("Sprites/Default");
            shader ??= Shader.Find("Universal Render Pipeline/Unlit");
            shader ??= Shader.Find("Standard");
            if (shader is null)
            {
                throw new System.InvalidOperationException("Could not resolve a shader for the rescue path marker.");
            }

            Material material = new Material(shader)
            {
                name = $"Generated_RescuePath_{ColorUtility.ToHtmlStringRGBA(color)}",
                color = color,
            };

            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_Blend", 0.0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (shader.name == "Standard")
            {
                material.SetFloat("_Mode", 3.0f);
            }

            return material;
        }

        private static void AttachOrUpdateBoardCellView(GameObject contentObject, TileCoord coord)
        {
            BoardCellView? cellView = contentObject.GetComponent<BoardCellView>();
            if (cellView is null)
            {
                cellView = contentObject.AddComponent<BoardCellView>();
            }

            cellView.Initialize(coord);
        }
    }
}
